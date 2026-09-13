# Repositorios y límites de persistencia

Los contratos pertenecen a **Domain** y sus implementaciones a **Infrastructure**. Application coordina casos de uso mediante esos contratos. La API compone el sistema llamando a `AddPersistence`, sin ejecutar SQL. Dapper simplifica la ejecución y el mapeo; no define las entidades ni las reglas de negocio.

| Responsabilidad | Ubicación |
|---|---|
| Lectura y escritura del agregado Battle | Domain/Battle/Repositories/IBattleReader e IBattleRepository |
| Contratos Species, CatalogMove y OwnedPokemon | Domain/Pokedex/Repositories |
| Lecturas coherentes y comandos atómicos de cualquier módulo | Domain/Common/Persistence/IReadSession e IUnitOfWork |
| SQL parametrizado y mapeo a agregados | Infrastructure/Persistence/Repositories |
| Conexión y transacción compartida | Infrastructure/Persistence/DatabaseSession |
| Formato de partidas y migraciones | Infrastructure/Persistence/Serialization y Migrations |
| Registro y arranque | Infrastructure/DependencyInjection |
| Comprobación de disponibilidad | Infrastructure/Persistence/PersistenceHealthCheck |

Infrastructure referencia Domain, sin depender de Application. Domain no conoce Dapper, Npgsql, configuración, hosting, DTO de aplicación ni HTTP. Las implementaciones concretas de repositorio son internas. Los contratos expresan operaciones del catálogo y de las partidas sin nombrar el proveedor de base de datos.

## Lecturas y escrituras separadas

Los handlers de Query reciben `IReadSession` y resuelven los lectores que necesitan dentro de su callback. No reciben el contrato de unidad de trabajo ni repositorios con operaciones de escritura. Los Commands reciben los puertos necesarios para modificar agregados. `IQuery<T>` e `ICommand<T>` identifican explícitamente cada solicitud de MediatR.

`IUnitOfWork` es común a todos los módulos; no existe una unidad por entidad o por Pokédex. Su callback recibe `IRepositoryScope`, que ofrece `GetRepository<T>()` para escritores y `GetReader<T>()` para lectores. La sesión de consulta expone únicamente `IReadRepositoryScope`. Los marcadores `IReadRepository` e `IWriteRepository` son independientes: no se puede pedir un escritor mediante el método genérico de lectura.

`RepositoryRegistry` concentra las fábricas en Infrastructure. `RepositoryScope` construye un repositorio al solicitarlo por primera vez y reutiliza esa instancia durante la misma operación. Al terminar, cierra la sesión; la siguiente operación tendrá sus propias instancias. Añadir un agregado requiere registrar su implementación, sin ampliar IUnitOfWork con nuevas propiedades. Esto está cubierto por una prueba que registra un repositorio adicional.

Cada contrato público tiene su propio archivo y describe las operaciones del agregado. La unidad de trabajo genérica coordina esos contratos; no impone un CRUD universal a todas las entidades.

La separación CQRS es lógica y usa la misma base de datos. No requiere dos bases, eventos ni consistencia eventual. La telemetría de MediatR vive en Application y puede observar un caso de uso aunque se invoque sin HTTP; API configura sus exportadores.

## Repositorio de partidas

`IBattleRepository` opera sobre el agregado completo. `Update` recibe una transformación del estado y exige una actualización atómica. El callback aplica reglas y no ejecuta E/S.

`BattleRepository` carga la partida mediante `SELECT FOR UPDATE` y ejecuta la acción. La unidad de trabajo común confirma una única versión nueva junto con el resto de cambios de la operación. Conexiones, Dapper, SQL, transacción y serialización permanecen en Infrastructure. Separar lectura y escritura en llamadas independientes perdería la protección frente a peticiones concurrentes.

Los movimientos y turnos de una partida son componentes internos de Battle; se persisten junto con su raíz. La [documentación de persistencia](persistencia.md) explica el documento JSONB versionado y su recuperación.

## Repositorios de Pokédex

Todos sus métodos de E/S son asíncronos y reciben cancelación. `FindAsync` busca por identidad, `FindManyAsync` resuelve relaciones en lotes y las consultas de existencia usan SQL `EXISTS`. Application proyecta los agregados recuperados a DTO de salida; no filtra un catálogo completo en memoria. Tampoco recibe `IQueryable`, conexiones ni objetos de Dapper.

`ReadAsync` abre una transacción PostgreSQL `REPEATABLE READ READ ONLY`. Los datos se consultan cuando el caso de uso los necesita. Una lectura de un movimiento no materializa ejemplares ni especies ajenas. Los repositorios cargan relaciones por conjuntos de identificadores, evitando una consulta adicional por cada elemento de una lista.

`WriteAsync` abre una transacción compartida por todos los repositorios solicitados. `CatalogDatabaseSession` adquiere el bloqueo del catálogo antes de su primera consulta dentro de un comando, previo a comprobar unicidad o referencias. Todas las llamadas `SaveAsync` y `DeleteAsync` ejecutan SQL dentro de esa transacción. Solo se publica el resultado si confirma todo el comando. Excepciones y cancelación anterior a la confirmación revierten las operaciones. Usar una sesión después de cerrarla falla explícitamente.

El bloqueo del catálogo no se adquiere al jugar un turno de una partida. Cuando se usa el catálogo, coordina escritores de distintas instancias y protege reglas entre agregados, por ejemplo modificar un plan sin invalidar movimientos aprendidos. Se conserva como decisión proporcionada a este catálogo: limita el paralelismo de escritura. La compatibilidad del aprendizaje está en `LearningPolicy`, dentro de Domain. Una futura optimización del bloqueo debe mantener esas garantías.

Las listas públicas aceptan `offset` y `limit` (predeterminados 0 y 100; máximo 100). `CatalogPage` valida los límites sin conocer HTTP ni SQL. Las lecturas por identidad y las reglas de integridad no dependen del tamaño de página.

Toda la aplicación usa PostgreSQL. Los dobles InMemory residen exclusivamente en `tests/Pokemon.Tests/Persistence`; no se publican en la imagen de la API. No hay caché de catálogo ni proveedor alternativo activado silenciosamente.

## Una operación con varios repositorios

```csharp
await unitOfWork.WriteAsync(async repositories =>
{
    var moves = repositories.GetRepository<IMoveRepository>();
    var battles = repositories.GetRepository<IBattleRepository>();
    await moves.SaveAsync(move, token);
    await battles.Add(battle, token);
    return battle.Id;
}, token);
```

`move` y `battle` representan agregados ya validados. Ambos repositorios usan la misma conexión y transacción. Si la segunda escritura falla, también se revierte la primera. Los handlers esperan cada llamada antes de continuar: una conexión transaccional no se usa para consultas paralelas. La creación de partidas sigue este mismo límite y combina lectura de participantes y escritura de Battle en una sola operación.

## Organización de excepciones

Domain agrupa excepciones en `Battle/Exceptions`, `Pokedex/Exceptions` y `Common/Exceptions`, con un archivo por clase. Las excepciones de Application se agrupan también en sus carpetas Exceptions. `PokedexGuard` está separado de las excepciones porque contiene validaciones, no representa un error. La API traduce las excepciones a ProblemDetails sin exponer detalles de SQL.

## Verificación

Las pruebas de arquitectura comprueban dependencias entre ensamblados, ubicación de los contratos, separación de lectura y escritura, marcadores CQRS, independencia de los DTO de salida y ausencia de persistencia en memoria en Infrastructure.

Las pruebas PostgreSQL comprueban rollback, cancelación, lectura coherente durante otra confirmación, sesiones cerradas, consultas selectivas, paginación, parámetros con comillas, concurrencia y recuperación. Los dobles de pruebas no sustituyen esa validación del SQL real.

Dapper está fijado a [2.1.79](https://www.nuget.org/packages/Dapper/2.1.79); la [documentación oficial](https://github.com/DapperLib/Dapper) describe sus operaciones de consulta y ejecución. Npgsql sigue siendo el proveedor PostgreSQL subyacente.
