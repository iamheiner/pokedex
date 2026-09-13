param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/authentication.ps1"
$BaseUrl = $BaseUrl.TrimEnd('/')
function Request($method, $path, $body = $null) {
    $options = @{ Method=$method; Uri="$BaseUrl$path"; Headers=(Get-PokemonAuthorizationHeaders); TimeoutSec=20 }
    if ($null -ne $body) { $options.ContentType='application/json'; $options.Body=$body | ConvertTo-Json -Depth 15 }
    Invoke-RestMethod @options
}
function Wait-Ready {
    for ($attempt=0; $attempt -lt 40; $attempt++) {
        try { Request GET /health/ready | Out-Null; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw 'API did not become ready'
}
$prefix = 'persistent-' + [Guid]::NewGuid().ToString('N')
$moves = [Collections.Generic.List[object]]::new()
$species = $null
$pokemon = $null
try {
    foreach ($number in 1..4) { $moves.Add((Request POST /moves @{name="$prefix-$number";power=40;type='Normal'})) }
    $species = Request POST /species @{
        name=$prefix; type='Normal'
        stats=@{health=40;attack=50;defense=60;specialAttack=70;specialDefense=80;speed=90}
        learnset=@($moves | ForEach-Object { @{moveId=$_.id;level=1} })
    }
    $pokemon = Request POST /pokemon @{
        speciesId=$species.id; name=$prefix; level=20; currentHealth=0; totalHealth=40; moveIds=@($moves | ForEach-Object {$_.id})
    }
    $before = (Request GET "/pokemon/$($pokemon.id)") | ConvertTo-Json -Depth 20 -Compress
    docker compose stop api
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop API' }
    try {
        docker compose up -d --force-recreate --wait postgres
        if ($LASTEXITCODE -ne 0) { throw 'Could not recreate PostgreSQL' }
    }
    finally {
        docker compose start api
        if ($LASTEXITCODE -ne 0) { throw 'Could not start API' }
    }
    Wait-Ready
    $after = (Request GET "/pokemon/$($pokemon.id)") | ConvertTo-Json -Depth 20 -Compress
    if ($before -cne $after) { throw 'Pokemon, species or learned moves changed after restart' }
    $savedSpecies = Request GET "/species/$($species.id)"
    if ($savedSpecies.name -ne $prefix -or $savedSpecies.learnset.Count -ne 4) { throw 'Species learnset did not survive' }
    foreach ($move in $moves) {
        if ((Request GET "/moves/$($move.id)").name -ne $move.name) { throw 'Move did not survive' }
    }
    Write-Output 'PostgreSQL persistence verified: custom Pokemon, health, species, learnset and four moves survive API restart and database container recreation.'
}
finally {
    if ($pokemon) { Request DELETE "/pokemon/$($pokemon.id)" | Out-Null }
    if ($species) { Request DELETE "/species/$($species.id)" | Out-Null }
    foreach ($move in $moves) { Request DELETE "/moves/$($move.id)" | Out-Null }
}
