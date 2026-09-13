param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/authentication.ps1"
$BaseUrl = $BaseUrl.TrimEnd('/')
$headers = Get-PokemonAuthorizationHeaders
$document = Invoke-RestMethod "$BaseUrl/openapi/v1.json" -Headers $headers -TimeoutSec 20
$count = 0
foreach ($path in $document.paths.PSObject.Properties) {
    $url = $BaseUrl + ($path.Name -replace '\{[^}]+\}', '00000000-0000-0000-0000-000000000201')
    foreach ($operation in $path.Value.PSObject.Properties) {
        if ($operation.Name -notin @('get','post','put','delete','patch','head','options')) { continue }
        $response = Invoke-WebRequest $url -Method $operation.Name -SkipHttpErrorCheck -TimeoutSec 20
        if ($response.StatusCode -ne 401) { throw "Anonymous $($operation.Name) $($path.Name): expected 401, got $($response.StatusCode)" }
        $count++
    }
}
$invalid = Invoke-WebRequest "$BaseUrl/pokemon" -Headers @{Authorization='Bearer invalid'} -SkipHttpErrorCheck -TimeoutSec 20
if ($invalid.StatusCode -ne 401) { throw 'Invalid token was accepted' }
$health = Invoke-WebRequest "$BaseUrl/health/ready" -Headers $headers -TimeoutSec 20
if ($health.StatusCode -ne 200) { throw 'Authenticated readiness failed' }
$scalar = Invoke-WebRequest "$BaseUrl/scalar/v1" -Headers $headers -TimeoutSec 20
if ($scalar.StatusCode -ne 200) { throw 'Authenticated Scalar failed' }
Write-Output "Authentication verified: $count operations reject anonymous requests; invalid token rejected; real Keycloak token opens readiness, OpenAPI and Scalar."
