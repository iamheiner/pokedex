# Partidas duraderas con PostgreSQL

Los ejemplos HTTP requieren un token Bearer de Keycloak. Antes de ejecutar los scripts, configura `POKEMON_CLIENT_SECRET` o `POKEMON_ACCESS_TOKEN` como explica [autenticación](autenticacion.md). Scalar dispone de login interactivo.

Las partidas se almacenan en PostgreSQL 17. Se recuperan por su mismo identificador después de reiniciar la API o recrear el contenedor de base de datos conservando su volumen. Se guardan participantes, características, movimientos, salud, usos, versión, resultado e historial.

La Pokédex conserva su adaptador en memoria. Sus ediciones se pierden al reiniciar, pero una partida existente sigue funcionando porque conserva sus propias copias de los adversarios. El cambio de persistencia de partidas no altera el contrato de los tres endpoints de combate.

## Arranque

```powershell
$env:POKEMON_API_PORT = '51966'
docker compose up --build -d
```

Compose crea PostgreSQL en la red interna, espera a que esté disponible y arranca la API. Las migraciones se aplican antes de aceptar peticiones. El volumen nombrado `pokemon-postgres` conserva los datos. `docker compose down` conserva el volumen; **añadir `-v` elimina el volumen y sus partidas**. La persistencia no sustituye a una copia de seguridad.

La configuración de Compose está destinada a desarrollo local. `.env.example` contiene los valores de demostración y puede copiarse a `.env` para cambiarlos; este último está excluido de Git. La contraseña inicial solo se aplica al crear un volumen vacío: cambiar la variable después no cambia por sí solo la contraseña del rol ya existente.

Para ejecutar la API con `dotnet run` y la base de datos en Docker, el override opcional publica PostgreSQL únicamente en loopback:

```powershell
docker compose -f compose.yaml -f compose.dev.yaml up -d postgres
$env:ConnectionStrings__Battles = 'Host=localhost;Port=54329;Database=pokemon;Username=pokemon;Password=pokemon_local_only;GSS Encryption Mode=Disable'
docker compose up -d --wait keycloak
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Authentication__DocumentationClientSecret = 'pokemon_docs_local_only'
dotnet run --project src/Pokemon.Api --no-launch-profile --urls http://localhost:5080 --ApiDocumentation:Enabled=true
```

El ejemplo usa la credencial local predeterminada; si se cambia, la conexión de la API debe usar el mismo valor. Sin conexión configurada, el modo Postgres falla al arrancar: no se cambia a memoria silenciosamente. Para una ejecución deliberadamente efímera, puede seleccionarse `--BattlePersistence:Provider=Memory`; las pruebas HTTP generales usan ese modo explícito.

## Modelo almacenado y DDD

El dominio no depende de Npgsql, SQL ni serialización. PostgresBattleRepository implementa el puerto IBattleRepository en Infrastructure. Se utiliza SQL parametrizado con Npgsql 10.0.3; no se añade un ORM para este único agregado.

La tabla `battles` tiene clave primaria UUID, versión positiva, documento JSONB y fechas de creación/actualización con zona horaria. Las restricciones verifican identidad, versión y formato del documento. La clave primaria cubre las consultas por identidad; no se añaden índices JSON que no necesita el acceso actual.

Una partida es una unidad de consistencia: salud, usos e historial se leen y guardan juntos. Sus adversarios son snapshots, por lo que no se crean claves foráneas a un catálogo que puede cambiar o eliminar ejemplares. No es necesario normalizar cada acción en otra tabla para las consultas actuales.

BattleDocumentCodec usa un formato explícito `formatVersion: 1`. Guarda las características iniciales y las acciones resueltas, incluidos sus factores aleatorios. Al cargar, reconstruye el agregado a través de sus reglas y verifica que cada resultado coincide con el guardado. No consulta la Pokédex ni obtiene nuevo azar. Esto conserva las invariantes sin añadir setters de persistencia al dominio.

El historial tiene menos de 60 acciones, de modo que esta reconstrucción está acotada. No se ha introducido una plataforma de event sourcing: sigue siendo un documento completo por partida. Si cambia la fórmula, los usos o cualquier regla que afecte al historial, deberá mantenerse el lector de reglas v1 o migrar/versionar el documento; cambiar las reglas sin tratar los datos existentes no es compatible. Los documentos inconsistentes fallan como errores de almacenamiento, no como errores de entrada del cliente.

## Transacciones y concurrencia

Cada acción abre una transacción y lee su fila con `SELECT ... FOR UPDATE`. Valida expectedVersion antes de obtener el azar y escribir. Dos instancias de la API que operen sobre la misma partida quedan coordinadas por PostgreSQL; una petición obsoleta recibe 409 sin aplicar otro ataque.

El bloqueo afecta a la partida seleccionada. Las lecturas normales ven el último estado confirmado y otras partidas pueden avanzar. Se actualizan versión, documento y fecha en una misma transacción. Una excepción antes del commit revierte los cambios.

No se reintenta automáticamente una escritura con resultado incierto. Si se pierde la conexión durante la confirmación, el cliente consulta la versión de la partida antes de reenviar una acción. Esta es la misma regla de recuperación del contrato de combate.

## Migraciones, disponibilidad y observabilidad

`Battle/Postgres/Migrations/001_battles.sql` crea el esquema inicial. BattleDatabaseMigrator registra las versiones aplicadas en `battle_schema_migrations` y coordina arranques simultáneos mediante un bloqueo transaccional de migración. Ejecutarlo de nuevo no borra datos. Los cambios posteriores de esquema deben añadirse como migraciones nuevas, sin editar una ya entregada.

La API no arranca si no puede aplicar o comprobar las migraciones. `/health` indica que el proceso responde; `/health/ready` comprueba PostgreSQL y el esquema y devuelve 503 cuando no están disponibles. Las consultas SQL emiten spans de Npgsql correlacionados con los handlers de MediatR y visibles en Aspire, sin registrar los valores de sus parámetros.

Compose usa un rol propietario para facilitar la prueba local. En un despliegue real convendría separar el rol de migraciones del rol de ejecución, configurar secretos, copias de seguridad y una política de retención. El volumen local no proporciona alta disponibilidad.

## Verificación reproducible

Suite sin base de datos (562 casos):

```powershell
dotnet test PokemonTwo.slnx -c Release --filter 'Category!=Postgres'
```

Suite contra PostgreSQL real (8 casos):

```powershell
docker compose run --build --rm postgres-tests
```

Los casos reales crean esquemas temporales independientes y solo eliminan esos esquemas al terminar. Comprueban migraciones simultáneas, restricciones, recuperación desde otra conexión y otra API, continuación de una partida, partidas terminadas, escrituras concurrentes, bloqueo por fila y rollback/cancelación. No requieren publicar el puerto de PostgreSQL. Sin `POKEMON_POSTGRES_TEST_CONNECTION`, esos ocho casos se omiten explícitamente; no deben contarse como verificados por la suite sin base de datos. El workflow de GitHub incluye ambas ejecuciones.

Para verificar recreación del contenedor y reinicio real de la API:

```powershell
./scripts/verify-battle-persistence.ps1 -BaseUrl http://localhost:51966
```

El script crea una partida, ejecuta un turno, detiene la API, recrea PostgreSQL con el mismo volumen, arranca la API y compara todo el estado recuperado. Después juega el siguiente turno. Durante esta comprobación la API queda temporalmente detenida.

Las partidas antiguas que solo existían en memoria no se migran automáticamente. Se ha conservado e importado la partida de demostración que estaba identificada antes del cambio; el resto de la persistencia comienza con las partidas guardadas mediante el nuevo adaptador.

## Referencias

La implementación utiliza [NpgsqlDataSource, parámetros y transacciones](https://www.npgsql.org/doc/basic-usage.html), [bloqueos por fila de PostgreSQL](https://www.postgresql.org/docs/17/explicit-locking.html) y [trazas Npgsql con OpenTelemetry](https://www.npgsql.org/doc/diagnostics/tracing.html).

Verificación realizada: ambas suites pasaron (562 + 8 casos), la recreación de PostgreSQL conservó el volumen y se recuperó íntegramente una partida en versión 2, que continuó en versión 3. La recreación posterior de la API también conservó esa versión. Readiness respondió 200 y no quedaron esquemas temporales de pruebas. El workflow remoto todavía no se ha ejecutado.
