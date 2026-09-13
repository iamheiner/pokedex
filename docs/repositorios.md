# Repositorios y límites de persistencia

Los contratos de repositorio pertenecen a **Domain** y sus implementaciones a **Infrastructure**. Application coordina casos de uso mediante los contratos de dominio. La API compone el sistema llamando a AddPersistence, sin ejecutar SQL.

| Responsabilidad | Ubicación |
|---|---|
| Contrato del agregado Battle | Domain/Battle/Repositories/IBattleRepository |
| Contratos Species, CatalogMove y OwnedPokemon | Domain/Pokedex/Repositories |
| Coordinación transaccional de Pokédex | Domain/Pokedex/Repositories/IPokedexUnitOfWork |
| SQL, mapeo JSONB y bloqueo por partida | Infrastructure/Battle/Postgres |
| Repositorios PostgreSQL de Pokédex y snapshot transaccional | Infrastructure/Pokedex/Postgres |
| Registro de persistencia y servicio de migración | Infrastructure/DependencyInjection |
| Consulta SQL de readiness | Infrastructure/Battle/Postgres/BattleDatabaseHealthCheck |

Infrastructure referencia Domain, sin depender de Application. Domain no conoce Npgsql, configuración, hosting, DTO de aplicación ni HTTP. BattleNotFoundException también pertenece al dominio para que el repositorio no lance una excepción definida en una capa exterior.

## Repositorio de partidas

IBattleRepository opera sobre el agregado completo: añade, recupera y actualiza partidas. La operación Update exige una actualización atómica. Su callback aplica reglas sobre el agregado cargado y no ejecuta E/S.

PostgresBattleRepository carga la partida con SELECT FOR UPDATE dentro de una transacción, ejecuta la acción y confirma una única versión nueva. El SQL parametrizado, las conexiones, la transacción y la serialización permanecen en Infrastructure. Mantener este límite evita separar lectura y escritura en llamadas independientes que perderían la protección frente a peticiones concurrentes.

No se crea un repositorio para cada movimiento o turno de una partida: son componentes internos de Battle y se persisten junto con su raíz.

## Repositorios de Pokédex

Species, CatalogMove y OwnedPokemon tienen contratos específicos. IPokedexUnitOfWork coordina esos tres repositorios: una operación publica todos sus cambios o ninguno. Las implementaciones trabajan sobre un snapshot privado; una excepción, cancelación o referencia a una sesión ya terminada no puede modificar el estado publicado.

IPokedexReader expone las colecciones del snapshot coherente para las consultas CQRS y las reglas entre agregados. Los filtros y proyecciones de Application se aplican sobre objetos ya cargados, sin IQueryable ni SQL. La carga, copia y almacenamiento están en Infrastructure.

Los contratos de los repositorios de Pokédex son síncronos porque trabajan dentro de ese snapshot; la unidad de trabajo asíncrona delimita la E/S y el acceso al estado. Si se incorpora un catálogo SQL grande, habrá que diseñar consultas paginadas y carga selectiva en Infrastructure, en lugar de trasladar automáticamente el catálogo entero a memoria.

Toda la aplicación usa PostgreSQL: partidas y Pokédex. Las clases InMemory están exclusivamente en tests/Pokemon.Tests/Persistence. El contenedor publicado no incluye implementaciones de persistencia en memoria.

## Verificación de arquitectura

PersistenceArchitectureTests impide dependencias exteriores desde Domain, dependencias hacia Application/API desde Infrastructure y uso directo de comandos/conexiones de base de datos desde API, Application o Domain. También comprueba que los contratos estén en Domain.

Las pruebas funcionales cubren CRUD, aislamiento de snapshots, commit/rollback conjunto de los tres repositorios, concurrencia de partidas y recuperación con PostgreSQL real.
