#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$ApiPort = $(if ($env:POKEMON_API_PORT) { [int]$env:POKEMON_API_PORT } else { 5080 }),
    [ValidateRange(1, 65535)]
    [int]$KeycloakPort = $(if ($env:KEYCLOAK_PORT) { [int]$env:KEYCLOAK_PORT } else { 18080 }),
    [ValidateRange(1, 65535)]
    [int]$DashboardPort = $(if ($env:ASPIRE_DASHBOARD_PORT) { [int]$env:ASPIRE_DASHBOARD_PORT } else { 18888 })
)

# La carga con punto conserva $api y la función de autenticación en la terminal del usuario.
if ($MyInvocation.InvocationName -ne '.') {
    throw 'Inicializa la sesión con punto y espacio: . ./scripts/initialize.ps1'
}

$initializationErrorPreference = $ErrorActionPreference
$ErrorActionPreference = 'Stop'
$initializationRoot = Split-Path -Parent $PSScriptRoot
Push-Location $initializationRoot
try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker no está disponible. Abre una terminal con acceso a Docker y vuelve a ejecutar el script.'
    }
    $initializationDockerOs = docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0 -or $initializationDockerOs -ne 'linux') {
        throw 'Inicia Docker con contenedores Linux y vuelve a ejecutar el script.'
    }

    $env:POKEMON_API_PORT = [string]$ApiPort
    $env:KEYCLOAK_PORT = [string]$KeycloakPort
    $env:ASPIRE_DASHBOARD_PORT = [string]$DashboardPort
    $api = "http://localhost:$ApiPort"
    $env:POKEMON_AUTHORITY = "http://localhost:$KeycloakPort/realms/pokemon"

    # Resolver Compose permite respetar también la credencial configurada mediante .env.
    $initializationConfigText = docker compose config --format json
    if ($LASTEXITCODE -ne 0) { throw 'No se ha podido resolver la configuración de Compose.' }
    $initializationConfig = ($initializationConfigText -join "`n") | ConvertFrom-Json
    $env:POKEMON_CLIENT_SECRET = [string]$initializationConfig.services.keycloak.environment.POKEMON_CLIENT_SECRET
    $initializationConfigText = $null
    $initializationConfig = $null
    . "$PSScriptRoot/authentication.ps1"

    Write-Host 'Preparando la API, PostgreSQL, Keycloak y Aspire...'
    docker compose up --build -d --wait
    if ($LASTEXITCODE -ne 0) {
        throw 'No se han podido iniciar los servicios. Revisa la salida de Docker y los puertos configurados.'
    }

    $initializationReady = $false
    foreach ($initializationAttempt in 1..60) {
        try {
            $initializationResponse = Invoke-WebRequest "$api/health/ready" -TimeoutSec 3 -SkipHttpErrorCheck
            if ($initializationResponse.StatusCode -eq 200) { $initializationReady = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $initializationReady) {
        throw 'La API no está preparada. Revisa: docker compose logs --tail 50 api'
    }

    try {
        $null = Invoke-RestMethod "$api/species?limit=1" -Headers (Get-PokemonAuthorizationHeaders) -TimeoutSec 20
    } catch {
        throw 'Los servicios están iniciados, pero ha fallado la consulta autenticada. Revisa las credenciales de Keycloak en docs/autenticacion.md.'
    }

    Write-Host "Proyecto preparado. API: $api"
    Write-Host "Scalar: $api/scalar/v1"
    Write-Host "Keycloak: http://localhost:$KeycloakPort"
    Write-Host "Aspire: http://localhost:$DashboardPort"
    Write-Host 'La sesión contiene $api y Get-PokemonAuthorizationHeaders. Ya puedes ejecutar los ejemplos del README.'
} finally {
    Pop-Location
    $ErrorActionPreference = $initializationErrorPreference
}
