# Autenticación con Keycloak

Todos los endpoints de la API requieren autenticación. La seguridad se aplica en el límite HTTP, en `Pokemon.Api/Feature/Authentication`; el dominio y los casos de uso mantienen su independencia de Keycloak.

## Arranque y acceso a Scalar

```powershell
# Revisar .env.example; contiene valores exclusivamente locales.
# Opcional: copiarlo a .env para usar el puerto 51966.
Copy-Item .env.example .env
docker compose up --build -d
```

Abre http://localhost:51966/scalar/v1 si usas ese archivo, o http://localhost:5080/scalar/v1 con el puerto predeterminado. Scalar redirige a Keycloak. La cuenta de demostración es **trainer**, con contraseña **trainer_local_only** (variable `POKEMON_TRAINER_PASSWORD`). Tras iniciar sesión, Scalar recibe el access token del usuario y lo utiliza en **Test Request**.

Keycloak está en http://localhost:18080. Para administrar el entorno local, usuario **admin** y contraseña **admin_local_only** (`KEYCLOAK_ADMIN_PASSWORD`). Estas credenciales de demostración y `start-dev` solo sirven para la prueba en loopback.

## Usar la API desde scripts

El cliente confidencial `pokemon-tools` está habilitado para `client_credentials`: permite automatizar la prueba sin usar contraseñas de usuarios ni habilitar el password grant en los clientes de la API.

```powershell
$env:POKEMON_CLIENT_SECRET = 'pokemon_client_local_only'
. ./scripts/authentication.ps1
$headers = Get-PokemonAuthorizationHeaders

Invoke-RestMethod http://localhost:51966/pokemon -Headers $headers
Invoke-RestMethod http://localhost:51966/damage -Method Post -Headers $headers `
  -ContentType application/json -InFile docs/damage-request.json

./scripts/verify-authentication.ps1 -BaseUrl http://localhost:51966
./scripts/verify-pokedex.ps1 -BaseUrl http://localhost:51966
./scripts/verify-battle.ps1 -BaseUrl http://localhost:51966
./scripts/verify-battle-persistence.ps1 -BaseUrl http://localhost:51966
$env:POKEMON_TRAINER_PASSWORD = 'trainer_local_only'
./scripts/verify-documentation-login.ps1 -BaseUrl http://localhost:51966
```

También se acepta `POKEMON_ACCESS_TOKEN` para usar un token obtenido previamente, y `POKEMON_AUTHORITY` para indicar otro realm. Si hay un token explícito, el helper lo utiliza hasta que el operador lo sustituya; no intenta renovarlo. Con client credentials obtiene tokens nuevos. Los scripts no imprimen ni guardan tokens en archivos.

## Qué valida la API

`AuthenticationFeature` registra JWT Bearer y una política global tanto por defecto como de respaldo. Una nueva ruta queda protegida aunque no declare `RequireAuthorization`. Se comprueban:

- Firma RSA SHA-256 contra las claves públicas obtenidas por descubrimiento OIDC/JWKS.
- Emisor del realm configurado y audiencia `pokemon-api`.
- Caducidad obligatoria y fecha de inicio de validez, con 30 segundos de tolerancia de reloj.
- Tokens firmados: se rechazan tokens manipulados, sin firma o con otros algoritmos.

Se usa el encabezado `Authorization: Bearer <access_token>`. Ni una cookie ni un token en la URL autentican operaciones de negocio o sondas de salud. Sin token o con token inválido se devuelve **401** y `WWW-Authenticate: Bearer`, sin revelar el motivo criptográfico al cliente.

La política cubre daño, CRUD, consultas, partidas y `/health` y `/health/ready`. Las sondas externas deben enviar un token válido. Las sondas internas de los contenedores de PostgreSQL y Keycloak pertenecen a esos servicios, no a la API Pokémon.

## Por qué la documentación tiene una cookie

Un navegador no envía un encabezado Bearer al escribir una URL. Para mantener Scalar utilizable, `DocumentationAuthentication` limita una segunda política a Scalar, sus recursos JavaScript y OpenAPI:

1. Sin credenciales, redirige al login del realm.
2. Usa Authorization Code con PKCE, cliente confidencial y callback exacto `/signin-oidc`.
3. Valida el callback mediante OIDC, estado, correlación, nonce y firma del proveedor.
4. Crea una cookie HttpOnly y entrega a esa sesión el access token para las llamadas de Scalar.
5. Limita la cookie a la vida del token, sin renovación deslizante, y marca la documentación como `Cache-Control: no-store`.

`/signin-oidc` es el punto de retorno del protocolo, procesado por el middleware OIDC: debe poder recibir el código antes de que exista una sesión. No expone datos de negocio ni permite crear una sesión sin validar el intercambio. Un callback falsificado devuelve 400.

Una petición de documentación con Bearer también se admite. Si ese Bearer es inválido devuelve 401; no recurre a una cookie para ocultar el fallo.

Scalar añade `AllowAnonymous` a sus recursos estáticos. La convención final del grupo elimina expresamente esa excepción. Una prueba recorre **todas las rutas registradas** y comprueba que ninguna conserve metadatos de acceso anónimo.

Al caducar el access token, recarga Scalar para iniciar de nuevo el flujo con Keycloak. No se solicita `offline_access` ni se implementa renovación de tokens en segundo plano. Tras recrear la API puede ser necesario recargar la documentación porque las claves de su cookie son efímeras; las cuentas y sesiones de Keycloak sí persisten.

## Docker, persistencia y configuración

Keycloak usa PostgreSQL separado (`keycloak-db`) y el volumen `keycloak-postgres`. Sus usuarios, clientes, sesiones y claves sobreviven a `docker compose down` y a la recreación de contenedores conservando los volúmenes. Las partidas siguen en su propia base y volumen `pokemon-postgres`.

El realm `pokemon` se importa desde `docker/keycloak/pokemon-realm.json` solo si todavía no existe. Cambiar el JSON, contraseñas de .env o puertos no actualiza un realm ya importado. Para instalaciones existentes, modifica clientes, credenciales y redirect URIs en Keycloak; conserva los volúmenes. En concreto, al cambiar el puerto de la API actualiza la URI permitida de `pokemon-documentation`.

| Configuración de API | Propósito |
|---|---|
| `Authentication:Authority` | Emisor público, por ejemplo `https://identity.example/realms/pokemon` |
| `Authentication:Audience` | Destinatario esperado: `pokemon-api` |
| `Authentication:MetadataAddress` | Descubrimiento interno opcional para Docker |
| `Authentication:RequireHttpsMetadata` | `true` por defecto; Compose local lo desactiva explícitamente |
| `Authentication:DocumentationClientSecret` | Secreto del cliente confidencial de documentación, requerido cuando se habilita |

La URL pública local es `http://localhost:18080`, pero la API consulta el descubrimiento interno en `http://keycloak:8080`. `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` permite resolver los endpoints de comunicación interna manteniendo el emisor público. No se desactiva la comprobación del emisor para solventar la red Docker.

Para ejecutar la API desde el SDK contra Keycloak local:

```powershell
docker compose -f compose.yaml -f compose.dev.yaml up -d --wait postgres keycloak
$env:ConnectionStrings__Battles = 'Host=localhost;Port=54329;Database=pokemon;Username=pokemon;Password=pokemon_local_only;GSS Encryption Mode=Disable'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Authentication__DocumentationClientSecret = 'pokemon_docs_local_only'
dotnet run --project src/Pokemon.Api --no-launch-profile --urls http://localhost:5080
```

La URI de retorno registrada debe coincidir con ese puerto. Fuera del entorno local hay que usar HTTPS, `start` de Keycloak, secretos propios suministrados por el despliegue, redirect URIs exactas y acceso restringido a la consola administrativa. La API falla al arrancar si falta emisor/audiencia o si se configura un emisor HTTP sin habilitarlo expresamente.

## Alcance de autorización

Este cambio autentica a usuarios y clientes; los recursos de la prueba siguen siendo compartidos. No se ha introducido propiedad de partidas por usuario ni roles de administrador/lector, porque sería una regla de negocio adicional. El realm emite tokens con audiencia de esta API solo a los clientes configurados para ello.

Los access tokens duran cinco minutos y se validan localmente. Cerrar sesión en Keycloak no revoca de forma instantánea un JWT ya emitido: sigue siendo válido hasta su caducidad. No hay dependencia de Valkey ni consulta de introspección por petición.

## Verificación

Las pruebas HTTP usan el validador JWT de producción con una clave RSA efímera y metadatos estáticos en el host de pruebas. No se sustituye la autenticación por un handler que acepte cualquier petición. Se cubren firma, algoritmo, emisor, audiencia, fechas, callback falsificado, aislamiento de cookies, caché y protección de todas las rutas.

Los scripts verifican además Keycloak real: client credentials, acceso autenticado, rechazo anónimo y flujo OIDC completo. La sonda OIDC emula la excepción de cookies Secure en HTTP localhost que aplican los navegadores; no cambia la configuración del servidor. También se ha probado el login y renderizado en un navegador real.

Referencias: [JWT Bearer en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Keycloak en contenedores](https://www.keycloak.org/server/containers), [hostname y backchannel](https://www.keycloak.org/server/hostname), [Scalar para ASP.NET Core](https://scalar.com/products/api-references/integrations/aspnetcore/integration).
