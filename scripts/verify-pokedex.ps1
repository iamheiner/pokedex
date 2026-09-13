param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/authentication.ps1"
$BaseUrl = $BaseUrl.TrimEnd('/')
$prefix = 'smoke-' + [Guid]::NewGuid().ToString('N')
$moveIds = [Collections.Generic.List[string]]::new()
$speciesId = $null
$pokemonId = $null
function Request($method, $path, $body, $expected) {
    $options = @{ Method = $method; Uri = "$BaseUrl$path"; SkipHttpErrorCheck = $true; Headers = (Get-PokemonAuthorizationHeaders); TimeoutSec = 20 }
    if ($null -ne $body) { $options.ContentType = 'application/json'; $options.Body = $body | ConvertTo-Json -Depth 15 }
    $response = Invoke-WebRequest @options
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { $response.Content }
    if ([int]$response.StatusCode -ne $expected) { throw "$method $path expected $expected, got $($response.StatusCode): $text" }
    if ($text) { return $text | ConvertFrom-Json }
}
try {
    foreach ($index in 1..5) {
        $move = Request POST /moves @{ name = "$prefix-$index"; power = 40; type = 'Normal' } 201
        $moveIds.Add([string]$move.id)
    }
    $learnset = for ($index = 0; $index -lt 5; $index++) {
        @{ moveId = $moveIds[$index]; level = $(if ($index -eq 4) { 30 } else { 1 }) }
    }
    $species = Request POST /species @{
        name = $prefix; type = 'Fire'; learnset = @($learnset)
        stats = @{ health = 50; attack = 60; defense = 70; specialAttack = 80; specialDefense = 90; speed = 100 }
    } 201
    $speciesId = [string]$species.id
    $input = @{ speciesId = $speciesId; name = $prefix; level = 20; currentHealth = 50; totalHealth = 50; moveIds = @($moveIds | Select-Object -First 4) }
    $pokemon = Request POST /pokemon $input 201
    $pokemonId = [string]$pokemon.id
    $learned = Request GET "/pokemon/$pokemonId/moves" $null 200
    if ($learned.moves.Count -ne 4) { throw 'Expected four learned moves' }
    $possible = Request GET "/pokemon/$pokemonId/possible-moves" $null 200
    if ($possible.moves.Count -ne 5) { throw 'Expected all five possible moves, including the future move' }
    $shared = @(Request GET "/moves/$($moveIds[0])/pokemon" $null 200)
    if ($shared.Count -ne 1 -or $shared[0].id -ne $pokemonId) { throw 'Incorrect inverse query' }
    $conflict = Request DELETE "/moves/$($moveIds[0])" $null 409
    if (-not $conflict.traceId) { throw 'Missing error traceId' }
    $input.currentHealth = 0
    $updated = Request PUT "/pokemon/$pokemonId" $input 200
    if ($updated.currentHealth -ne 0) { throw 'Health update was not stored' }
    Write-Output 'Pokédex HTTP: CRUD, learned/possible/shared moves, zero health and reference protection verified.'
}
finally {
    if ($pokemonId) { Request DELETE "/pokemon/$pokemonId" $null 204 | Out-Null }
    if ($speciesId) { Request DELETE "/species/$speciesId" $null 204 | Out-Null }
    foreach ($moveId in $moveIds) { Request DELETE "/moves/$moveId" $null 204 | Out-Null }
}
