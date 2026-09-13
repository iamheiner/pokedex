param(
    [string]$BaseUrl = 'http://localhost:5080',
    [Guid]$FirstPokemonId = '00000000-0000-0000-0000-000000000201',
    [Guid]$SecondPokemonId = '00000000-0000-0000-0000-000000000202'
)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
function Request($method, $path, $body, $expected) {
    $options = @{ Method = $method; Uri = "$BaseUrl$path"; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $options.ContentType = 'application/json'; $options.Body = $body | ConvertTo-Json -Depth 15 }
    $response = Invoke-WebRequest @options
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { $response.Content }
    if ([int]$response.StatusCode -ne $expected) { throw "$method $path expected $expected, got $($response.StatusCode): $text" }
    if ($text) { return $text | ConvertFrom-Json }
}
$firstBefore = Request GET "/pokemon/$FirstPokemonId" $null 200
$secondBefore = Request GET "/pokemon/$SecondPokemonId" $null 200
$battle = Request POST /battles @{ firstPokemonId = $FirstPokemonId; secondPokemonId = $SecondPokemonId } 201
while ($battle.phase -eq 'AwaitingAction' -and $battle.turns.Count -lt 60) {
    $actor = if ($battle.nextPokemonId -eq $battle.first.id) { $battle.first } else { $battle.second }
    $move = $actor.moves | Where-Object { $_.remainingUses -gt 0 } | Select-Object -First 1
    $moveId = if ($null -eq $move) { $null } else { $move.id }
    $expectedVersion = $battle.version
    $battle = Request POST "/battles/$($battle.id)/turns" @{
        pokemonId = $actor.id; moveId = $moveId; expectedVersion = $expectedVersion
    } 200
    if ($battle.version -ne $expectedVersion + 1) { throw 'A turn must advance exactly one version' }
    $turn = $battle.turns[-1]
    Write-Output "Turn $($turn.number): $($turn.moveName), damage $($turn.appliedDamage); health $($battle.first.currentHealth)/$($battle.second.currentHealth)"
}
if ($battle.phase -ne 'Finished' -or ($battle.first.currentHealth -gt 0 -and $battle.second.currentHealth -gt 0)) {
    throw 'Battle did not finish with a Pokemon at zero health'
}
$saved = Request GET "/battles/$($battle.id)" $null 200
if ($saved.version -ne $battle.version) { throw 'Final state was not saved' }
$problem = Request POST "/battles/$($battle.id)/turns" @{
    pokemonId = $battle.first.id; moveId = $battle.first.moves[0].id; expectedVersion = $battle.version
} 409
if (-not $problem.traceId) { throw 'Expected conflict traceId' }
$firstAfter = Request GET "/pokemon/$FirstPokemonId" $null 200
$secondAfter = Request GET "/pokemon/$SecondPokemonId" $null 200
if ($firstAfter.currentHealth -ne $firstBefore.currentHealth -or $secondAfter.currentHealth -ne $secondBefore.currentHealth) {
    throw 'Battle must not change collection health'
}
Write-Output "Battle $($battle.id) finished. Winner: $($battle.winnerId); draw: $($battle.isDraw). Collection unchanged."
