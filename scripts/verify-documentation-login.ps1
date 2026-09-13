param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
if (-not $env:POKEMON_TRAINER_PASSWORD) { throw 'Set POKEMON_TRAINER_PASSWORD for the local trainer account.' }
$base = [Uri]$BaseUrl
$authority = if ($env:POKEMON_AUTHORITY) { [Uri]$env:POKEMON_AUTHORITY } else { [Uri]'http://localhost:18080/realms/pokemon' }
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(20)

function Read-LoginPage([Uri]$Uri, [Net.Http.HttpContent]$Body = $null) {
    for ($redirect = 0; $redirect -lt 10; $redirect++) {
        if ($Uri.GetLeftPart([UriPartial]::Authority) -notin @($base.GetLeftPart([UriPartial]::Authority), $authority.GetLeftPart([UriPartial]::Authority))) {
            throw 'Login redirects to an unexpected origin'
        }
        # Keycloak usa cookies Secure incluso en localhost. Los navegadores consideran
        # localhost un contexto seguro; HttpClient no. Emulamos esa excepción SOLO en
        # esta sonda local, nunca en el servidor ni para dominios externos.
        if ($Uri.Scheme -eq 'http' -and $Uri.Host -eq 'localhost') {
            foreach ($cookie in $handler.CookieContainer.GetAllCookies()) {
                if ($cookie.Domain -eq 'localhost') { $cookie.Secure = $false }
            }
        }
        $method = if ($Body) { [Net.Http.HttpMethod]::Post } else { [Net.Http.HttpMethod]::Get }
        $request = [Net.Http.HttpRequestMessage]::new($method, $Uri)
        if ($Body) { $request.Content = $Body }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        if ([int]$response.StatusCode -in @(301,302,303)) {
            $Uri = [Uri]::new($Uri, $response.Headers.Location)
            $response.Dispose()
            $Body = $null
            continue
        }
        $response.EnsureSuccessStatusCode() | Out-Null
        return @{ Uri=$Uri; Content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
    }
    throw 'Too many login redirects'
}
try {
    $page = Read-LoginPage "$BaseUrl/scalar/v1"
    $match = [regex]::Match($page.Content, '<form[^>]+action="([^"]+)"')
    if (-not $match.Success) { throw 'Keycloak login form was not returned' }
    $action = [Uri][Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
    if ($action.GetLeftPart([UriPartial]::Authority) -ne $authority.GetLeftPart([UriPartial]::Authority)) {
        throw 'Login form targets an unexpected identity provider'
    }
    $fields = [Collections.Generic.Dictionary[string,string]]::new()
    $fields.Add('username', 'trainer')
    $fields.Add('password', $env:POKEMON_TRAINER_PASSWORD)
    $page = Read-LoginPage $action ([Net.Http.FormUrlEncodedContent]::new($fields))
    if (-not $page.Uri.AbsoluteUri.StartsWith("$BaseUrl/scalar")) { throw 'OIDC did not return to Scalar' }
    $token = [regex]::Match($page.Content, '"token"s*:s*"(eyJ[^"]+)"').Groups[1].Value
    if (-not $token) { throw 'Scalar did not receive the signed-in user access token' }
    $client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    (Read-LoginPage "$BaseUrl/pokemon") | Out-Null
    $client.DefaultRequestHeaders.Authorization = $null
    (Read-LoginPage "$BaseUrl/openapi/v1.json") | Out-Null
    (Read-LoginPage "$BaseUrl/scalar/scalar.js") | Out-Null
    $response = $client.GetAsync("$BaseUrl/pokemon").GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne 401) { throw 'Documentation cookie authorized the business API' }
    Write-Output 'OIDC verified: login, callback, protected Scalar/schema/assets, user Bearer token and cookie isolation.'
}
finally { $client.Dispose() }
