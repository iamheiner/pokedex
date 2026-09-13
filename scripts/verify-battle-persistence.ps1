param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
function Wait-Ready {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Invoke-WebRequest "$BaseUrl/health/ready" -TimeoutSec 10 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'API did not become ready after restarting'
}
Wait-Ready
$battle = Invoke-RestMethod "$BaseUrl/battles" -Method Post -ContentType application/json -Body (@{
    firstPokemonId = '00000000-0000-0000-0000-000000000201'
    secondPokemonId = '00000000-0000-0000-0000-000000000202'
} | ConvertTo-Json)
$turn = @{ pokemonId = $battle.first.id; moveId = $battle.first.moves[0].id; expectedVersion = $battle.version }
$battle = Invoke-RestMethod "$BaseUrl/battles/$($battle.id)/turns" -Method Post -ContentType application/json -Body ($turn | ConvertTo-Json)
$before = $battle | ConvertTo-Json -Depth 20 -Compress
docker compose stop api
if ($LASTEXITCODE -ne 0) { throw 'Could not stop API' }
try {
    # Recrea el contenedor, conservando su volumen: verifica más que reiniciar un proceso.
    docker compose up -d --force-recreate --wait postgres
    if ($LASTEXITCODE -ne 0) { throw 'Could not recreate PostgreSQL' }
}
finally {
    docker compose start api
    if ($LASTEXITCODE -ne 0) { throw 'Could not start API' }
}
Wait-Ready
$recovered = Invoke-RestMethod "$BaseUrl/battles/$($battle.id)"
$after = $recovered | ConvertTo-Json -Depth 20 -Compress
if ($before -cne $after) { throw 'Recovered battle differs from the saved state' }
$actor = if ($recovered.nextPokemonId -eq $recovered.first.id) { $recovered.first } else { $recovered.second }
$turn = @{ pokemonId = $actor.id; moveId = $actor.moves[0].id; expectedVersion = $recovered.version }
$continued = Invoke-RestMethod "$BaseUrl/battles/$($battle.id)/turns" -Method Post -ContentType application/json -Body ($turn | ConvertTo-Json)
if ($continued.version -ne $battle.version + 1) { throw 'Recovered battle did not continue correctly' }
Write-Output "Persistence verified: battle $($battle.id), identical version $($battle.version) recovered; continued at version $($continued.version)."
