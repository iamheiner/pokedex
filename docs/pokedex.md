# Ejercicio 2: Pokédex

Antes de probar los ejemplos, ejecuta `. ./scripts/initialize.ps1` desde la raíz del proyecto, con el puerto que utilices. El [arranque del README](../README.md#inicializar-el-proyecto-y-empezar-a-probar) prepara los servicios y la autenticación de esa terminal. Scalar dispone de login interactivo.

Una especie es la ficha común de un Pokémon, como Charmander. Un ejemplar es uno concreto de tu colección, con nombre, nivel, salud y cuatro ataques elegidos. El catálogo de movimientos describe los ataques. El plan de aprendizaje relaciona una especie con los ataques que puede aprender y el nivel mínimo de cada uno.

## Recursos y requisitos

| Requisito | Endpoints |
| --- | --- |
| Pokémon base CRUD | `POST /species`, `GET /species`, `GET/PUT/DELETE /species/{id}` |
| Movimientos CRUD | `POST /moves`, `GET /moves`, `GET/PUT/DELETE /moves/{id}` |
| Mis Pokémon CRUD | `POST /pokemon`, `GET /pokemon`, `GET/PUT/DELETE /pokemon/{id}` |
| Pokémon con sus cuatro movimientos actuales | `GET /pokemon/{id}/moves` |
| Movimientos que puede llegar a aprender | `GET /pokemon/{id}/possible-moves` |
| Pokémon que comparten un movimiento aprendido | `GET /moves/{id}/pokemon` |
| Especies que pueden aprender un movimiento | `GET /moves/{id}/species` |

La última consulta hace explícita la otra interpretación de «compartir un movimiento». Las consultas de ejemplares devuelven sus datos y los cuatro movimientos completos. Las consultas de posibles incluyen el nivel mínimo y también los movimientos futuros; no se filtran por el nivel actual ni excluyen los ya aprendidos.

POST genera un identificador y devuelve 201 con Location. PUT reemplaza todos los datos editables y devuelve 200; no crea un recurso inexistente. DELETE devuelve 204. No existe PATCH. Una colección sin resultados devuelve 200 con una lista vacía; un identificador inexistente devuelve 404.

Todos los campos de entrada son obligatorios, incluidos los arrays vacíos admitidos para el plan de una especie. Los tipos se envían como cadenas (`Fire`, `Normal`). Los errores incluyen ProblemDetails y traceId: 400 entrada inválida, 404 recurso inexistente, 409 nombre duplicado o referencia en uso, 415 contenido no admitido, 500 fallo interno.

## Probarlo

Arranca según el README y abre Scalar. Encontrarás los grupos Pokedex - Species, Moves y Pokemon. Los POST y PUT incluyen ejemplos de entrada; los de especies y ejemplares utilizan los identificadores del catálogo inicial.

```powershell
$api = 'http://localhost:51966'
$env:POKEMON_CLIENT_SECRET = 'pokemon_client_local_only'
. ./scripts/authentication.ps1
Invoke-RestMethod -Headers (Get-PokemonAuthorizationHeaders) "$api/species"
Invoke-RestMethod -Headers (Get-PokemonAuthorizationHeaders) "$api/pokemon/00000000-0000-0000-0000-000000000201/moves"
Invoke-RestMethod -Headers (Get-PokemonAuthorizationHeaders) "$api/pokemon/00000000-0000-0000-0000-000000000201/possible-moves"
Invoke-RestMethod -Headers (Get-PokemonAuthorizationHeaders) "$api/moves/00000000-0000-0000-0000-000000000001/pokemon"
```

El primer ejemplar es Charmander de nivel 20. Tiene Scratch, Ember, Dragon Breath y Fire Fang aprendidos. La consulta de posibles también devuelve Flamethrower, disponible desde nivel 24. Dragon Breath es de tipo Dragon, aunque Charmander sea Fire: coincidir en tipo no determina el aprendizaje.

La comprobación de CRUD sobre una API real está en `scripts/verify-pokedex.ps1`. Requiere PowerShell 7, crea sus propios datos con nombres únicos y los elimina al finalizar:

```powershell
./scripts/verify-pokedex.ps1 -BaseUrl http://localhost:51966
```

## Reglas y decisiones DDD

- Species, CatalogMove y OwnedPokemon son agregados con identidad e invariantes propias. BaseStats y LearnableMove son objetos valor. No hay dependencias de HTTP, MediatR o infraestructura en el dominio.
- Un ejemplar debe tener exactamente cuatro identificadores de movimiento distintos, permitidos por su especie y nivel. No basta con que existan en el catálogo. Salud admite cero, pero nunca negativos ni valores superiores al total.
- Se conservan los límites del ejercicio 1: nivel 1–100, estadísticas y salud total 1–10000, potencia 1–250. El catálogo sigue acotado a ataques de potencia fija; no modela movimientos de estado ni potencia variable.
- Los nombres de especies y movimientos se recortan en los extremos, tienen 1–100 caracteres y son únicos sin distinguir mayúsculas. Los nombres de ejemplares pueden repetirse porque su identidad es el Guid.
- El plan de aprendizaje pertenece a la especie y se edita mediante su PUT. No puede contener movimientos desconocidos, nulos o duplicados. Una especie puede tener un plan vacío si no tiene ejemplares que dependan de él.
- No se puede eliminar una especie con ejemplares ni un movimiento incluido en un plan de aprendizaje. Tampoco se puede modificar un plan de manera que invalide movimientos ya aprendidos. La API devuelve 409 y conserva el estado anterior.
- Las referencias al catálogo son vivas: editar nombre, tipo o potencia de un movimiento se refleja en las siguientes consultas de ejemplares. Editar una especie modifica sus datos base compartidos; la salud actual y total de cada ejemplar se conservan.
- Combatant y Move siguen siendo los datos inmutables que recibe la calculadora del ejercicio 1. Sus reglas de máximo cuatro movimientos no cambian; OwnedPokemon aplica la regla más estricta de exactamente cuatro del ejercicio 2. El ejercicio 3 toma una instantánea al iniciar una partida para que los cambios del catálogo no alteren un combate en curso.

## CQRS, persistencia y concurrencia

Cada caso de uso tiene su Command o Query y su handler bajo Application/Feature/Pokedex. La API solo adapta HTTP y envía a MediatR. Las proyecciones de salida se separan de los agregados y los contratos de entrada.

`IReadSession` ofrece consultas coherentes y `IUnitOfWork` agrupa comprobaciones y cambios atómicos. Ambos contratos están en Domain. Infrastructure los implementa con Dapper, PostgreSQL y una transacción compartida por operación. Los filtros se ejecutan en SQL y las relaciones se recuperan en lotes. La [arquitectura del README](../README.md#cómo-está-organizada-la-solución) explica los límites entre capas.

Los datos sobreviven al reinicio de la API y de PostgreSQL conservando su volumen. Un bloqueo transaccional del catálogo coordina escritores de distintas instancias. Los PUT concurrentes se serializan: la última escritura válida prevalece, sin versión del cliente para el catálogo. Los combates sí tienen control de versión.

Las listas de movimientos, especies, ejemplares y relaciones inversas admiten `?offset=0&limit=100`. `offset` no puede ser negativo y `limit` debe estar entre 1 y 100. Una página fuera del catálogo devuelve un array vacío. Las listas generales ordenan por nombre y, en caso de empate, por identidad; las relaciones inversas ordenan por identidad. Cada petición ve una lectura coherente, aunque distintas páginas pueden variar si el catálogo cambia entre peticiones.

El catálogo inicial se inserta una sola vez durante la migración. Los dobles en memoria solo existen en el proyecto de pruebas. Valkey se incorporaría si aparece una necesidad medida de caché.

«Mis Pokémon» representa una colección compartida de demostración. Todos los endpoints requieren autenticación Keycloak; no se ha añadido propiedad por usuario ni un sistema multitenant. Las operaciones heredan trazas, logs y métricas de MediatR, visibles en Aspire.

## Datos iniciales y fuentes

Se cargan cinco especies, cinco ejemplares de nivel 20 y 21 movimientos durante la primera migración. Las cinco especies tienen un único tipo en su forma normal. Sus estadísticas son las documentadas en los ejemplos del ejercicio 1. La salud inicial igual a la estadística base es una simplificación; no se calcula crecimiento por nivel.

| Especie | Sufijo de ID de especie | Sufijo de ID de ejemplar | Fuente del aprendizaje |
| --- | --- | --- | --- |
| Charmander | 101 | 201 | [Scarlet/Violet](https://pokemondb.net/pokedex/charmander/moves/9) |
| Squirtle | 102 | 202 | [Scarlet/Violet](https://pokemondb.net/pokedex/squirtle/moves/9) |
| Pikachu | 103 | 203 | [Scarlet/Violet](https://pokemondb.net/pokedex/pikachu/moves/9) |
| Sandshrew | 104 | 204 | [Scarlet/Violet, forma normal](https://pokemondb.net/pokedex/sandshrew/moves/9) |
| Eevee | 105 | 205 | [Scarlet/Violet](https://pokemondb.net/pokedex/eevee/moves/9) |

Se incluye un subconjunto de movimientos aprendidos por nivel, no el catálogo oficial completo. Los niveles y las potencias de los ataques seleccionados proceden de esas tablas. Se omiten efectos secundarios, prioridad y mecánicas de acumulación de daño. Los datos están versionados en PokedexSeed.cs y se insertan en PostgreSQL al inicializar el esquema; no requieren consultar una API externa. Los seis escenarios del ejercicio 1 siguen siendo ejemplos independientes del catálogo persistido en PostgreSQL.

## Verificación

Las pruebas de Pokédex cubren los tres CRUD, Location, campos obligatorios, proyecciones, cuatro movimientos, incompatibilidad y nivel, referencias, edición compartida, rollback, cancelación, aislamiento de colecciones, altas concurrentes, ejemplos OpenAPI ejecutables y uso de un ejemplar en POST /damage sin modificar su salud. Se ejecutan junto con las pruebas del ejercicio 1.

El [ejercicio 3](combate.md) utiliza los ejemplares de esta Pokédex para crear partidas independientes.

Los comandos para repetir las pruebas están en el [README](../README.md#ejecutar-las-pruebas).
