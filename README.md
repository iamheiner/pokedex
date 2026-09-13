# Pokémon: daño, Pokédex y combate

Backend de los **tres ejercicios** de la prueba técnica, con .NET 10, DDD, CQRS/MediatR, Dapper, PostgreSQL, Docker, Keycloak, Scalar y OpenTelemetry. Incluye cálculo de daño, Pokédex con CRUD de especies, movimientos y ejemplares, y partidas por turnos hasta alcanzar salud cero. El [recorrido de entrega](docs/entrega.md) relaciona cada requisito con su implementación y verificación.

La Pokédex arranca con cinco especies y cinco ejemplares con cuatro movimientos cada uno. Especies, movimientos, Pokémon y partidas se guardan en PostgreSQL y sobreviven al reinicio. El catálogo inicial solo se inserta al crear el esquema. Configuración y garantías en [persistencia](docs/persistencia.md).

Todos los endpoints requieren autenticación con **Keycloak**, incluida la documentación; las sondas `/health` y `/health/ready` son la única excepción anónima para que orquestadores y balanceadores puedan consultarlas. Scalar redirige al login (usuario local `trainer`, contraseña `trainer_local_only`). Las operaciones de negocio exigen un access token Bearer. Configuración, ejemplos y límites en [autenticación](docs/autenticacion.md).

## Arranque con Docker

Requiere Docker con contenedores Linux. Compose incluye las bases PostgreSQL de partidas y de Keycloak con volúmenes persistentes.

```powershell
# Opcional si el puerto predeterminado 5080 está ocupado:
$env:POKEMON_API_PORT = '51966'
docker compose up --build -d
```

| Servicio | Puerto predeterminado | Con el ejemplo anterior |
|---|---|---|
| Scalar | http://localhost:5080/scalar/v1 | http://localhost:51966/scalar/v1 |
| API | http://localhost:5080 | http://localhost:51966 |
| Keycloak | http://localhost:18080 | http://localhost:18080 |
| Aspire | http://localhost:18888 | http://localhost:18888 |

Después de iniciar sesión con Keycloak, en Scalar selecciona **Damage → Test Request** y uno de los seis ejemplos. Los grupos **Pokedex** permiten probar los CRUD y consultas; **Battle** permite crear partidas y resolver sus turnos. Los POST de creación incluyen ejemplos. El puerto de Aspire se configura mediante `ASPIRE_DASHBOARD_PORT`. Para detener los servicios: `docker compose down`.

Compose habilita la documentación y el dashboard para uso local. La API y la UI solo se publican en loopback. OTLP circula por la red interna de Docker. El dashboard admite acceso anónimo y pierde los datos al reiniciarse; no es una configuración de publicación pública ni de retención de producción.

## Ejecución y pruebas con el SDK

Requiere cualquier SDK .NET 10 (10.0.100 o superior); `global.json` acepta versiones menores y parches posteriores.

```powershell
dotnet test PokemonTwo.slnx -c Release --filter 'Category!=Postgres'
docker compose -f compose.yaml -f compose.dev.yaml up -d --wait postgres keycloak
$env:ConnectionStrings__Battles = 'Host=localhost;Port=54329;Database=pokemon;Username=pokemon;Password=pokemon_local_only;GSS Encryption Mode=Disable'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Authentication__DocumentationClientSecret = 'pokemon_docs_local_only'
# La API utiliza PostgreSQL también al ejecutarse con el SDK.
dotnet run --project src/Pokemon.Api --no-launch-profile --urls http://localhost:5080 --ApiDocumentation:Enabled=true
```

La URI de retorno del cliente de documentación debe coincidir con el puerto usado. Sin `OTEL_EXPORTER_OTLP_ENDPOINT` la API funciona sin collector. Para una demostración con Aspire, usar Compose.

```powershell
$env:POKEMON_CLIENT_SECRET = 'pokemon_client_local_only'
. ./scripts/authentication.ps1
Invoke-RestMethod http://localhost:5080/damage -Method Post -Headers (Get-PokemonAuthorizationHeaders) `
  -ContentType application/json -InFile docs/damage-request.json
```

El ejemplo es Squirtle contra Charmander: efectividad 2, daño entre 33 y 39 y factor aleatorio entre 85 y 100. Los demás escenarios y sus fuentes están en [ejemplos](docs/ejemplos.md).

## Contrato HTTP

- `POST /damage`: devuelve `damage`, `effectiveness` y `randomFactor`, sin modificar salud.
- `/species`, `/moves`, `/pokemon`: CRUD del ejercicio 2. Las rutas, consultas de aprendizaje, reglas y ejemplos están en [Pokédex](docs/pokedex.md).
- `POST /battles`, `GET /battles/{id}`, `POST /battles/{id}/turns`: combate con estado, historial y control de versión. Reglas y demostración en [combate](docs/combate.md).
- `GET /health`: estado del proceso; `GET /health/ready`: disponibilidad de PostgreSQL y su esquema.
- `GET /openapi/v1.json`: contrato OpenAPI; Scalar en `/scalar/v1`.

Todos los campos del JSON son obligatorios. Tipos como `Fire` o `Water` se envían como texto. Un campo omitido no recibe un valor por defecto; se rechazan referencias nulas, propiedades desconocidas, valores fuera de rango y movimientos no aprendidos. Salud cero enviada explícitamente sí es válida para el cálculo teórico.

Errores en formato `application/problem+json`, con `status`, `title`, `detail` y `traceId`: 401 para token ausente o inválido, 400 para entrada inválida, 404 para recurso inexistente, 409 para duplicados o referencias en uso, 415 para tipo de contenido no admitido y 500 para fallos internos. El cliente no recibe detalles internos del servidor.

## Estructura

```text
src/
  Pokemon.Domain/                 # Daño, agregados Pokedex y Battle
  Pokemon.Application/
    Common/Exceptions/           # Errores esperados del caso de uso
    Feature/Damage/
      Queries/CalculateDamage/   # Query y handler
      IDamageRandom.cs
    Feature/Pokedex/             # Commands, Queries, contratos y puerto transaccional
    Feature/Battle/              # Crear, consultar y resolver partidas
  Pokemon.Infrastructure/
    Pokedex/                     # Repositorios PostgreSQL, unidad de trabajo y migración inicial
    Battle/                      # Repositorio PostgreSQL, formato durable y migraciones
  Pokemon.Api/
    Middleware/                  # Contrato uniforme de errores HTTP
    Feature/Damage/              # Endpoint, contratos y seis ejemplos
    Feature/Pokedex/             # HTTP de Species, Moves y Pokemon
    Feature/Battle/              # Contrato HTTP de partidas y turnos
    Feature/Health/
    Observability/               # Trazas, logs y métricas
    Program.cs                   # Configuración y composición
tests/Pokemon.Tests/            # Dominio, matriz, CQRS y HTTP real
```

Flujo: HTTP → ISender → comportamiento de telemetría → handler de Command/Query → dominio y puerto de almacenamiento. Domain no depende de ASP.NET, MediatR ni OpenTelemetry. Los agregados protegen sus invariantes; los handlers coordinan cada caso de uso. La organización por Feature admite nuevos casos de uso sin acumularlos en Program.cs. Cada operación HTTP dispone de su propio archivo en `Commands` o `Queries` dentro de su recurso, con métodos separados para registrar la ruta y atender la petición. Véase la [organización de endpoints](docs/endpoints.md).

## Decisiones principales

La fórmula sigue el PDF con decimal y redondeo hacia abajo al final. Se compara el tipo del movimiento con el del defensor. Se utiliza un solo tipo por Pokémon y ataque/defensa normales; no se replican todas las mecánicas oficiales. El daño puede ser cero y no se limita a la salud restante.

MediatR se fija exactamente a **12.5.0**, con [licencia Apache 2.0](https://github.com/jbogard/MediatR/blob/v12.5.0/LICENSE), sin clave comercial. Una actualización requiere revisar licencia y compatibilidad. Microsoft.OpenApi se fija en una versión corregida del aviso GHSA-v5pm-xwqc-g5wc, sin deshabilitar la auditoría NuGet.

La Pokédex usa PostgreSQL con operaciones atómicas y restricciones relacionales; no requiere caché. Sus límites y evolución están en [decisiones de Pokédex](docs/pokedex.md). El cálculo conserva sus [decisiones de diseño](docs/decisiones.md).

## Verificación

La suite contiene **604 casos sin base de datos y 20 contra PostgreSQL real**. Las pruebas cubren los 324 cruces de efectividad con datos independientes del código, fórmula con estadísticas asimétricas, extremos y redondeo, invariantes del modelo, seis ejemplos por HTTP, campos obligatorios, errores y cancelación. La integración usa el host real de ASP.NET y el registro real de MediatR. La Pokédex añade pruebas de CRUD, consultas, referencias, aprendizaje, concurrencia y rollback. El combate cubre partidas completas, snapshots, versiones, agotamiento, inmunidades, esfuerzo y finalización.

La imagen multietapa solo compila y publica la API con usuario no privilegiado; las pruebas se ejecutan con el SDK, en CI y, para PostgreSQL real, con el perfil `postgres-tests` de Compose. Se pueden obtener datos de cobertura con:

```powershell
dotnet test PokemonTwo.slnx -c Release --filter "Category!=Postgres" --collect:"XPlat Code Coverage" --results-directory TestResults
```

En Aspire, el servicio `pokemon-api` muestra el POST y el span `CalculateDamageQuery` con el mismo TraceId, logs correlacionados y métricas `pokemon.requests`/`pokemon.request.duration`. Las futuras Queries/Commands heredan la instrumentación; las operaciones PostgreSQL añaden spans de Npgsql.

El [informe de revisión](docs/revision-enunciado.md) conserva los hallazgos iniciales y su cierre para dar trazabilidad a las correcciones.

Las dependencias resueltas se guardan en `packages.lock.json`; Docker y el workflow de GitHub restauran en modo bloqueado. `.github/workflows/verify.yml` queda preparado para ejecutar pruebas y construir la imagen al subir el proyecto a GitHub. El workflow remoto todavía no se ha ejecutado.

El repositorio sigue [Git Flow](docs/git-flow.md), con ramas de entrega e integración.

Los recorridos HTTP reproducibles están en `scripts/verify-pokedex.ps1` y `scripts/verify-battle.ps1`; admiten `-BaseUrl` para usar el puerto elegido.

La suite PostgreSQL se ejecuta con `docker compose run --build --rm postgres-tests`. El script `scripts/verify-battle-persistence.ps1` comprueba la recuperación tras recrear PostgreSQL y reiniciar la API. `docker compose down` conserva las partidas; `docker compose down -v` elimina sus datos.

La separación de capas y las garantías de persistencia están explicadas en [repositorios y unidad de trabajo](docs/repositorios.md).

La [revisión arquitectónica](docs/revision-arquitectura.md) explica los contratos independientes del proveedor, los repositorios Dapper, la segregación CQRS y las comprobaciones realizadas. Las listas de Pokédex admiten `offset` y `limit` (0 y 100 por defecto; máximo 100).
