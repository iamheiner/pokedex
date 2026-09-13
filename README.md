# Pokémon: cálculo de daño

Backend del **ejercicio 1** de la prueba técnica, con .NET 10, DDD, CQRS/MediatR, Docker, Scalar y OpenTelemetry. Calcula daño teórico a partir de un atacante, uno de sus movimientos y un defensor. Los ejercicios 2 (Pokédex) y 3 (combate con estado) quedan fuera de esta entrega.

## Arranque con Docker

Requiere Docker con contenedores Linux.

```powershell
# Opcional si el puerto predeterminado 5080 está ocupado:
$env:POKEMON_API_PORT = '51966'
docker compose up --build -d
```

| Servicio | Puerto predeterminado | Con el ejemplo anterior |
|---|---|---|
| Scalar | http://localhost:5080/scalar/v1 | http://localhost:51966/scalar/v1 |
| API | http://localhost:5080 | http://localhost:51966 |
| Aspire | http://localhost:18888 | http://localhost:18888 |

En Scalar selecciona **Damage → Test Request** y uno de los seis ejemplos. El puerto de Aspire se configura mediante `ASPIRE_DASHBOARD_PORT`. Para detener los servicios: `docker compose down`.

Compose habilita la documentación y el dashboard para uso local. La API y la UI solo se publican en loopback. OTLP circula por la red interna de Docker. El dashboard admite acceso anónimo y pierde los datos al reiniciarse; no es una configuración de publicación pública ni de retención de producción.

## Ejecución y pruebas sin Docker

Requiere SDK .NET 10.0.302 o compatible según `global.json`.

```powershell
dotnet test PokemonTwo.slnx -c Release
dotnet run --project src/Pokemon.Api --no-launch-profile --urls http://localhost:5080 --ApiDocumentation:Enabled=true
```

Sin `OTEL_EXPORTER_OTLP_ENDPOINT` la API funciona sin collector. Para una demostración con Aspire, usar Compose.

```powershell
curl.exe -H "Content-Type: application/json" --data-binary @docs/damage-request.json http://localhost:5080/damage
```

El ejemplo es Squirtle contra Charmander: efectividad 2, daño entre 33 y 39 y factor aleatorio entre 85 y 100. Los demás escenarios y sus fuentes están en [ejemplos](docs/ejemplos.md).

## Contrato HTTP

- `POST /damage`: devuelve `damage`, `effectiveness` y `randomFactor`, sin modificar salud.
- `GET /health`: estado del proceso.
- `GET /openapi/v1.json`: contrato OpenAPI; Scalar en `/scalar/v1`.

Todos los campos del JSON son obligatorios. Tipos como `Fire` o `Water` se envían como texto. Un campo omitido no recibe un valor por defecto; se rechazan referencias nulas, propiedades desconocidas, valores fuera de rango y movimientos no aprendidos. Salud cero enviada explícitamente sí es válida para el cálculo teórico.

Errores en formato `application/problem+json`, con `status`, `title`, `detail` y `traceId`: 400 para entrada inválida, 415 para tipo de contenido no admitido y 500 para fallos internos. El cliente no recibe detalles internos del servidor.

## Estructura

```text
src/
  Pokemon.Domain/                 # Combatant, Move, fórmula y efectividad
  Pokemon.Application/
    Common/Exceptions/           # Errores esperados del caso de uso
    Feature/Damage/
      Queries/CalculateDamage/   # Query y handler
      IDamageRandom.cs
  Pokemon.Api/
    Errors/                      # Contrato uniforme de errores HTTP
    Feature/Damage/              # Endpoint, contratos y seis ejemplos
    Feature/Health/
    Observability/               # Trazas, logs y métricas
    Program.cs                   # Configuración y composición
tests/Pokemon.Tests/            # Dominio, matriz, CQRS y HTTP real
```

Flujo: HTTP → ISender → comportamiento de telemetría → QueryHandler → dominio. Domain no depende de ASP.NET, MediatR ni OpenTelemetry. El agregado protege sus invariantes y la colección de movimientos; el handler coordina el cálculo. La organización por Feature admite nuevos casos de uso sin acumularlos en Program.cs.

## Decisiones principales

La fórmula sigue el PDF con decimal y redondeo hacia abajo al final. Se compara el tipo del movimiento con el del defensor. Se utiliza un solo tipo por Pokémon y ataque/defensa normales; no se replican todas las mecánicas oficiales. El daño puede ser cero y no se limita a la salud restante.

MediatR se fija exactamente a **12.5.0**, con [licencia Apache 2.0](https://github.com/jbogard/MediatR/blob/v12.5.0/LICENSE), sin clave comercial. Una actualización requiere revisar licencia y compatibilidad. Microsoft.OpenApi se fija en una versión corregida del aviso GHSA-v5pm-xwqc-g5wc, sin deshabilitar la auditoría NuGet.

No se necesita persistencia ni caché. Las decisiones, límites y evolución hacia especies, ejemplares y aprendizaje se explican en [decisiones de diseño](docs/decisiones.md).

## Verificación

La suite contiene **452 casos de prueba**. Las pruebas cubren los 324 cruces de efectividad con datos independientes del código, fórmula con estadísticas asimétricas, extremos y redondeo, invariantes del modelo, seis ejemplos por HTTP, campos obligatorios, errores y cancelación. La integración usa el host real de ASP.NET y el registro real de MediatR.

Docker ejecuta las pruebas antes de publicar una imagen multietapa con usuario no privilegiado. Se pueden obtener datos de cobertura con:

```powershell
dotnet test PokemonTwo.slnx -c Release --collect:"XPlat Code Coverage" --results-directory TestResults
```

En Aspire, el servicio `pokemon-api` muestra el POST y el span `CalculateDamageQuery` con el mismo TraceId, logs correlacionados y métricas `pokemon.requests`/`pokemon.request.duration`. Las futuras Queries/Commands heredan la instrumentación; la instrumentación de una futura base de datos se añadirá al incorporarla.

El [informe de revisión](docs/revision-enunciado.md) conserva los hallazgos iniciales y su cierre para dar trazabilidad a las correcciones.

Las dependencias resueltas se guardan en `packages.lock.json`; Docker y el workflow de GitHub restauran en modo bloqueado. `.github/workflows/verify.yml` queda preparado para ejecutar pruebas y construir la imagen al subir el proyecto a GitHub. El workflow remoto todavía no se ha ejecutado.

El repositorio sigue [Git Flow](docs/git-flow.md), con ramas de entrega e integración.
