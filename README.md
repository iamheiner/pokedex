# Pokémon: cálculo de daño, Pokédex y combate

Esta es mi solución a los tres ejercicios de la prueba técnica. La entrega contiene solo el backend, desarrollado en .NET 10: calcula el daño de un ataque, permite gestionar una colección de Pokémon y mantiene partidas por turnos hasta que uno de los participantes se queda sin salud.

He mantenido los tres ejercicios en la misma solución porque se apoyan entre sí. La Pokédex proporciona los participantes y sus movimientos; el combate utiliza esos datos y reutiliza el cálculo de daño. Las reglas de cada parte están separadas del transporte HTTP y del acceso a la base de datos.

No hace falta conocer el juego para probarla. Un **Pokémon** es un participante con nivel, salud y estadísticas. Un **movimiento** es un ataque que puede utilizar. El **tipo**, como fuego o agua, determina si ese ataque resulta más o menos eficaz contra el rival.

## Poner el proyecto en marcha

### Requisitos

- Git y Docker con contenedores Linux. En Windows, Docker Desktop debe estar iniciado.
- PowerShell 7 para ejecutar los ejemplos y los scripts de comprobación.
- El SDK de .NET 10 solo es necesario si quieres ejecutar la API o las pruebas fuera de Docker.

Todos los comandos siguientes se ejecutan desde la raíz del repositorio. Los bloques de PowerShell comparten las variables de la misma terminal.

```powershell
git clone https://github.com/iamheiner/pokedex.git
cd pokedex
```

Si ya tienes el proyecto descargado, abre una terminal en su carpeta y continúa aquí:

```powershell
$env:POKEMON_API_PORT = '5080'
$env:KEYCLOAK_PORT = '18080'
$env:ASPIRE_DASHBOARD_PORT = '18888'
$api = "http://localhost:$env:POKEMON_API_PORT"

docker compose up --build -d --wait
```

Si el puerto 5080 está ocupado, cambia `POKEMON_API_PORT` antes de arrancar, por ejemplo a `51966`. Los ejemplos seguirán usando `$api`. El puerto de la API también se utiliza al configurar el retorno del login de Scalar: si ya habías inicializado Keycloak con otro puerto, revisa [la configuración de autenticación](docs/autenticacion.md).

Compose levanta la API, PostgreSQL para los datos de la aplicación, Keycloak con su propia base de datos y el dashboard de Aspire. Al iniciar, la API aplica las migraciones e inserta el catálogo de ejemplo si el esquema todavía no existe.

Comprueba que ha terminado de arrancar:

```powershell
$ready = $false
foreach ($attempt in 1..60) {
    try {
        $response = Invoke-WebRequest "$api/health/ready" -TimeoutSec 3
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 1
}
if (-not $ready) { throw 'La API no está preparada. Revisa: docker compose logs --tail 50 api' }
```

| Servicio | Dirección con los puertos anteriores |
| --- | --- |
| Scalar, para probar la API desde el navegador | `http://localhost:5080/scalar/v1` |
| Keycloak | `http://localhost:18080` |
| Aspire, para consultar trazas, logs y métricas | `http://localhost:18888` |

Abre Scalar e inicia sesión con el usuario `trainer` y la contraseña `trainer_local_only`. Son credenciales de demostración para el entorno local.

Para llamar a la API desde los ejemplos de este README, prepara también la autenticación de la terminal:

```powershell
$env:POKEMON_CLIENT_SECRET = 'pokemon_client_local_only'
. ./scripts/authentication.ps1
```

`Get-PokemonAuthorizationHeaders` obtiene un token para el cliente `pokemon-tools`. Cada ejemplo solicita sus cabeceras al ejecutarse, de modo que no depende de un token copiado manualmente. Los ejemplos presuponen las credenciales predeterminadas de Compose; si las cambiaste, utiliza tus valores. Las opciones están en [autenticación](docs/autenticacion.md).

## Ejercicio 1. Cálculo de daño

### Qué se ha implementado

`POST /damage` recibe un atacante, un defensor y el nombre del movimiento que se quiere usar. Devuelve el daño calculado, la efectividad y el factor aleatorio aplicado. Esta operación no modifica la salud ni crea una partida.

En el dominio, `Combatant` representa los datos necesarios para el cálculo y `Move` contiene el nombre, la potencia y el tipo del ataque. `DamageCalculator` aplica la fórmula y `TypeEffectiveness` resuelve la tabla de tipos.

La comparación se hace entre **el tipo del movimiento y el tipo del defensor**. Por ejemplo, un ataque de agua contra un defensor de fuego tiene efectividad 2, aunque el Pokémon atacante tenga otro tipo.

La fórmula implementada es:

```text
daño = suelo(((2 × nivel / 5 + 2) × ataque × potencia / defensa / 50)
             × efectividad × factorAleatorio / 100)
```

Se calcula con `decimal` y se redondea hacia abajo una sola vez, al final. El factor aleatorio es un entero entre 85 y 100. La efectividad puede ser 0, 0,5, 1 o 2; una inmunidad produce cero daño. Se utiliza un solo tipo por Pokémon y las estadísticas de ataque y defensa normales.

El azar se obtiene en Application mediante `IDamageRandom`, por lo que las pruebas pueden fijar su valor. El cálculo es una Query de CQRS aunque se exponga mediante POST: recibe una entrada compleja, pero no cambia estado.

### Cómo comprobarlo

El archivo de ejemplo representa a Squirtle atacando a Charmander:

```powershell
Invoke-RestMethod "$api/damage" -Method Post `
    -Headers (Get-PokemonAuthorizationHeaders) `
    -ContentType 'application/json' -InFile ./docs/damage-request.json
```

El resultado debe tener `effectiveness = 2`, `randomFactor` entre 85 y 100 y `damage` entre 33 y 39. El número exacto puede cambiar entre peticiones. Scalar incluye seis ejemplos para probar debilidad, resistencia, inmunidad y otras combinaciones.

Las pruebas cubren la fórmula, sus límites y los **324 cruces de los 18 tipos**, con datos esperados independientes de la tabla implementada. El detalle de las simplificaciones está en [decisiones del cálculo](docs/decisiones.md) y los escenarios en [ejemplos](docs/ejemplos.md).

## Ejercicio 2. API tipo Pokédex

### Qué se ha implementado

La Pokédex distingue tres recursos:

| Recurso | Qué representa | Ruta |
| --- | --- | --- |
| Especie | La ficha común, sus estadísticas base y su plan de aprendizaje | `/species` |
| Movimiento | Un ataque del catálogo con nombre, potencia y tipo | `/moves` |
| Ejemplar | Un Pokémon concreto de la colección, con nivel, salud y cuatro movimientos elegidos | `/pokemon` |

Los tres permiten crear, listar, consultar por identificador, actualizar y eliminar. En el dominio corresponden a `Species`, `CatalogMove` y `OwnedPokemon`. Cada agregado protege sus reglas; Application coordina las comprobaciones que necesitan consultar otros recursos.

Un ejemplar debe tener **exactamente cuatro movimientos distintos**, permitidos para su especie y nivel. No basta con que los movimientos existan o coincidan con su tipo. Tampoco se permite borrar un movimiento que sigue en uso ni una especie que tiene ejemplares asociados.

Las consultas de movimientos tienen propósitos diferentes:

| Consulta | Qué devuelve |
| --- | --- |
| `GET /pokemon/{id}/moves` | El ejemplar con sus cuatro movimientos aprendidos |
| `GET /pokemon/{id}/possible-moves` | El plan de aprendizaje de su especie, con los niveles mínimos, incluidos los futuros |
| `GET /moves/{id}/pokemon` | Los ejemplares que tienen aprendido ese movimiento |
| `GET /moves/{id}/species` | Las especies que pueden aprenderlo |

Las listas principales admiten `offset` y `limit`: por defecto, 0 y 100; el límite máximo es 100. Los cambios se guardan mediante repositorios Dapper en una transacción de PostgreSQL.

### Cómo comprobarlo

El catálogo inicial contiene cinco especies, cinco ejemplares y 21 movimientos. El primer ejemplar es Charmander, de nivel 20:

```powershell
Invoke-RestMethod "$api/species?offset=0&limit=5" `
    -Headers (Get-PokemonAuthorizationHeaders)

$charmanderId = '00000000-0000-0000-0000-000000000201'
$learned = Invoke-RestMethod "$api/pokemon/$charmanderId/moves" `
    -Headers (Get-PokemonAuthorizationHeaders)
$possible = Invoke-RestMethod "$api/pokemon/$charmanderId/possible-moves" `
    -Headers (Get-PokemonAuthorizationHeaders)

$learned | ConvertTo-Json -Depth 8
$possible | ConvertTo-Json -Depth 8
```

Con el catálogo original, la primera respuesta contiene Scratch, Ember, Dragon Breath y Fire Fang. La segunda incluye también Flamethrower, que puede aprender desde el nivel 24. Esto permite ver la diferencia entre los movimientos actuales y los que podrá aprender más adelante.

Para recorrer el CRUD y comprobar las reglas con datos propios del script:

```powershell
./scripts/verify-pokedex.ps1 -BaseUrl $api
```

El script crea movimientos, una especie y un ejemplar con nombres únicos. Comprueba las consultas, una actualización de salud y el rechazo de borrados con referencias; después elimina los datos que ha creado. Los ejemplos con identificadores fijos dependen de que conserves el catálogo inicial. Las rutas y reglas completas están en [Pokédex](docs/pokedex.md).

## Ejercicio 3. Combate Pokémon

### Qué se ha implementado

Una partida enfrenta a dos ejemplares de la Pokédex. `Battle` es el agregado que controla los turnos, la salud, los movimientos disponibles y el resultado. Al crearla se copian los datos de los participantes: lo que ocurre durante el combate no modifica sus fichas en la colección.

La API expone tres operaciones:

| Operación | Propósito |
| --- | --- |
| `POST /battles` | Crear una partida con `firstPokemonId` y `secondPokemonId` |
| `GET /battles/{id}` | Consultar su estado e historial |
| `POST /battles/{id}/turns` | Enviar una acción con `pokemonId`, `moveId` y `expectedVersion` |

Empieza el participante con mayor velocidad; en empate, el primero de la petición. Después se alternan las acciones. Cada ataque normal reutiliza el cálculo del ejercicio 1 y descuenta como máximo la salud restante del defensor.

La partida pasa de `AwaitingAction` a `Finished` cuando uno de los participantes alcanza salud cero. Una vez terminada, rechaza nuevas acciones. El cliente debe enviar los turnos: la API no juega sola.

Cada movimiento dispone de cinco usos por partida. Cuando un participante los agota todos, puede enviar `moveId: null` para usar **esfuerzo**: una acción con daño y retroceso que permite terminar incluso si los ataques normales no causan daño. Son reglas simplificadas de esta solución, explicadas en [combate](docs/combate.md).

La versión aumenta con cada acción aceptada. Si dos peticiones intentan actuar sobre la misma versión, solo una se confirma; la otra recibe `409 Conflict`. La comprobación y el guardado se realizan dentro de una transacción con bloqueo por fila.

### Cómo comprobarlo

Con los ejemplares iniciales de Charmander y Squirtle:

```powershell
$body = @{
    firstPokemonId = '00000000-0000-0000-0000-000000000201'
    secondPokemonId = '00000000-0000-0000-0000-000000000202'
} | ConvertTo-Json

$battle = Invoke-RestMethod "$api/battles" -Method Post `
    -Headers (Get-PokemonAuthorizationHeaders) `
    -ContentType 'application/json' -Body $body

$actor = if ($battle.nextPokemonId -eq $battle.first.id) { $battle.first } else { $battle.second }
$turn = @{
    pokemonId = $actor.id
    moveId = $actor.moves[0].id
    expectedVersion = $battle.version
} | ConvertTo-Json

$battle = Invoke-RestMethod "$api/battles/$($battle.id)/turns" -Method Post `
    -Headers (Get-PokemonAuthorizationHeaders) `
    -ContentType 'application/json' -Body $turn

Invoke-RestMethod "$api/battles/$($battle.id)" `
    -Headers (Get-PokemonAuthorizationHeaders) | ConvertTo-Json -Depth 10
```

Tras la acción, la versión debe aumentar de 1 a 2 y el historial debe contener un turno. El daño concreto depende del azar.

Para ejecutar un combate completo:

```powershell
./scripts/verify-battle.ps1 -BaseUrl $api
```

El script elige movimientos, muestra las acciones, comprueba que la partida termina y verifica que no admite más turnos. También comprueba que la salud de la colección no ha cambiado. Las partidas de estos ejemplos quedan guardadas y pueden consultarse después.

## Cómo está organizada la solución

He separado las responsabilidades en cuatro proyectos:

```text
src/
  Pokemon.Domain/          Reglas, agregados y contratos de persistencia
  Pokemon.Application/     Commands, Queries y coordinación de casos de uso
  Pokemon.Infrastructure/  Repositorios Dapper, transacciones y migraciones
  Pokemon.Api/             Endpoints, autenticación y configuración HTTP
tests/
  Pokemon.Tests/           Pruebas de dominio, HTTP, arquitectura y persistencia
```

Domain no conoce ASP.NET, MediatR ni PostgreSQL. Los contratos de repositorio y de unidad de trabajo están en el dominio; sus implementaciones viven en `Infrastructure/Persistence`. La unidad de trabajo es compartida y crea los repositorios según se necesitan dentro de cada operación.

En Application, los Commands coordinan las escrituras mediante `IUnitOfWork` y las Queries acceden a los datos mediante `IReadSession`. MediatR conecta cada petición con su handler. La versión está fijada a 12.5.0.

La API se organiza por Feature y recurso. Cada operación tiene un archivo dentro de `Commands` o `Queries`; sus métodos registran la ruta y adaptan la petición HTTP. Por ejemplo: `Feature/Pokedex/Moves/Commands/CreateMoveEndpoint.cs`.

El recorrido habitual es **endpoint → MediatR → handler → dominio y repositorios**. Los detalles están en [repositorios y unidad de trabajo](docs/repositorios.md) y [organización de endpoints](docs/endpoints.md).

## Bonus: lo añadido sobre los ejercicios

### Persistencia después de reiniciar

El enunciado permite elegir el almacenamiento. He utilizado PostgreSQL tanto para la Pokédex como para las partidas, con migraciones y volúmenes Docker. Las partidas guardan una versión del documento y su historial para poder recuperarlas sin volver a generar números aleatorios.

Puedes comprobar la recuperación de una partida con:

```powershell
./scripts/verify-battle-persistence.ps1 -BaseUrl $api
```

Este script crea una partida, ejecuta un turno, detiene la API, recrea el contenedor PostgreSQL conservando el volumen y vuelve a arrancar la API. Compara el estado recuperado y continúa el combate. Durante la comprobación la API deja de estar disponible brevemente.

Para detener el entorno al terminar:

```powershell
docker compose down
```

Los datos permanecen en los volúmenes. Añadir `-v` a ese comando los elimina. El formato de las partidas y las garantías transaccionales están en [persistencia](docs/persistencia.md).

### Autenticación con Keycloak

Las operaciones de negocio exigen un token Bearer. Scalar utiliza un inicio de sesión OIDC para que se pueda abrir desde el navegador. Las únicas rutas anónimas son `/health` y `/health/ready`, destinadas a comprobar el estado de la aplicación.

El entorno incluye un usuario y un cliente de demostración. La autenticación no implementa propiedad de los recursos por usuario: los usuarios autenticados acceden al mismo catálogo y a las mismas partidas.

### Documentación interactiva y errores uniformes

Scalar permite explorar los contratos y ejecutar peticiones. Los endpoints incluyen resúmenes, descripciones y ejemplos donde corresponde; OpenAPI está disponible en `/openapi/v1.json`, también protegido.

Los errores se devuelven como `application/problem+json`, con `status`, `title`, `detail` y `traceId`. Se distingue entre entrada inválida (400), falta de autenticación (401), recurso inexistente (404), conflicto (409), contenido no admitido (415) y fallo interno (500). El manejador de `Api/Middleware` evita exponer detalles internos en las respuestas de error.

### Observabilidad con OpenTelemetry y Aspire

La API envía trazas, logs y métricas al dashboard de Aspire. Después de ejecutar un ejemplo, abre el servicio `pokemon-api` y busca su petición HTTP: el Command o Query y las operaciones de PostgreSQL quedan asociados a la misma traza.

Se utiliza el **dashboard de Aspire como contenedor**, sin un proyecto AppHost. El dashboard local admite acceso anónimo y no conserva su información al reiniciarse. Estas opciones y las credenciales de ejemplo están pensadas para una demostración local.

### Docker y comprobaciones automáticas

La imagen final ejecuta la API con un usuario no privilegiado. La construcción restaura las dependencias en modo bloqueado y publica la aplicación; las pruebas se ejecutan por separado.

El [workflow de GitHub Actions](.github/workflows/verify.yml) define la ejecución de pruebas, construcción de la imagen y comprobaciones con PostgreSQL y Keycloak. Su resultado debe consultarse en cada ejecución; disponer del archivo no implica que el pipeline haya pasado.

El repositorio usa `main` como rama principal y `develop` para integrar cambios. Las funcionalidades se trabajan en ramas separadas antes de incorporarlas.

## Ejecutar las pruebas

Desde la raíz del proyecto, con el SDK de .NET 10:

```powershell
dotnet restore PokemonTwo.slnx --locked-mode
dotnet test PokemonTwo.slnx -c Release --no-restore --filter 'Category!=Postgres'
```

Este grupo no necesita una base de datos. Incluye pruebas de reglas, fórmula, CQRS, contratos HTTP, autenticación y arquitectura. `InMemoryUnitOfWork` pertenece exclusivamente al proyecto de tests y permite aislar esas comprobaciones; la API utiliza PostgreSQL.

Para comprobar los repositorios y transacciones contra PostgreSQL real:

```powershell
docker compose run --build --rm postgres-tests
```

El contenedor usa la categoría `Postgres` y la conexión del entorno Compose. Estas pruebas complementan los dobles de memoria y comprueban el comportamiento del almacenamiento real.

**Última comprobación local (13/09/2026):** 614 pruebas sin PostgreSQL y 20 pruebas contra PostgreSQL superadas. El arranque con `docker compose up --build -d --wait` también se ha comprobado. Se han ejecutado el ejemplo de daño y los scripts HTTP de Pokédex y combate contra la API local. Estos resultados corresponden a esta revisión; los comandos anteriores permiten repetir las comprobaciones.

## Ejecutar la API desde el SDK

Esta alternativa sirve para depurar la aplicación manteniendo PostgreSQL y Keycloak en Docker. Conserva las variables de puertos del arranque inicial y detén la API del contenedor para liberar su puerto:

```powershell
docker compose stop api
docker compose -f compose.yaml -f compose.dev.yaml up -d --wait postgres keycloak

$env:ConnectionStrings__Battles = 'Host=localhost;Port=54329;Database=pokemon;Username=pokemon;Password=pokemon_local_only;GSS Encryption Mode=Disable'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Authentication__Authority = "http://localhost:$env:KEYCLOAK_PORT/realms/pokemon"
$env:Authentication__MetadataAddress = "$env:Authentication__Authority/.well-known/openid-configuration"
$env:Authentication__Audience = 'pokemon-api'
$env:Authentication__DocumentationClientSecret = 'pokemon_docs_local_only'
$env:Authentication__RequireHttpsMetadata = 'false'
$env:OTEL_EXPORTER_OTLP_ENDPOINT = ''

dotnet run --project src/Pokemon.Api --no-launch-profile --urls $api --ApiDocumentation:Enabled=true
```

Aunque la clave se llame `ConnectionStrings__Battles`, esa conexión sirve tanto para la Pokédex como para las partidas. Los valores anteriores corresponden a las credenciales y al puerto PostgreSQL predeterminados de Compose. Si los has personalizado, adapta también esta conexión.

La terminal queda ocupada por la API. Para pararla, pulsa `Ctrl+C`; para volver a la API del contenedor, ejecuta `docker compose start api`. En esta modalidad se desactiva la exportación OTLP; el arranque completo con Compose incluye la conexión al dashboard de Aspire.
