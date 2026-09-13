function Get-PokemonAuthorizationHeaders {
    if ($env:POKEMON_ACCESS_TOKEN) {
        return @{ Authorization = "Bearer $env:POKEMON_ACCESS_TOKEN" }
    }
    if (-not $env:POKEMON_CLIENT_SECRET) {
        throw 'Set POKEMON_CLIENT_SECRET (client credentials) or POKEMON_ACCESS_TOKEN before running this script.'
    }
    $authority = if ($env:POKEMON_AUTHORITY) { $env:POKEMON_AUTHORITY.TrimEnd('/') } else { 'http://localhost:18080/realms/pokemon' }
    $token = Invoke-RestMethod "$authority/protocol/openid-connect/token" -Method Post -TimeoutSec 20 -Body @{
        grant_type = 'client_credentials'
        client_id = 'pokemon-tools'
        client_secret = $env:POKEMON_CLIENT_SECRET
    }
    return @{ Authorization = "Bearer $($token.access_token)" }
}
