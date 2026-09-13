# Ejercicio 2: Pokédex

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
Invoke-RestMethod "$api/species"
Invoke-RestMethod "$api/pokemon/00000000-0000-0000-0000-000000000201/moves"
Invoke-RestMethod "$api/pokemon/00000000-0000-0000-0000-000000000201/possible-moves"
Invoke-RestMethod "$api/moves/00000000-0000-0000-0000-000000000001/pokemon"
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
- Combatant y Move siguen siendo los datos inmutables que recibe la calculadora del ejercicio 1. Sus reglas de máximo cuatro movimientos no cambian; OwnedPokemon aplica la regla más estricta de exactamente cuatro del ejercicio 2. El ejercicio 3 deberá tomar una instantánea al iniciar una partida para que los cambios del catálogo no alteren un combate en curso.

## CQRS, persistencia y concurrencia

Cada caso de uso tiene su Command o Query y su handler bajo Application/Feature/Pokedex. La API solo adapta HTTP y envía a MediatR. Las proyecciones de salida se separan de los agregados y los contratos de entrada.

IPokedexStore es el puerto de Application: Read ofrece una lectura coherente, Write agrupa comprobaciones y cambios en una transacción. Infrastructure implementa el puerto con memoria, copia del estado y un semáforo por almacén. Las entidades son inmutables; las colecciones del dominio son copias de solo lectura. Si un comando falla o se cancela antes de publicar, no se conserva ninguna modificación. Comprobación de unicidad y escritura se ejecutan juntas, incluso bajo peticiones concurrentes.

Esta implementación está pensada para un catálogo pequeño en un único proceso. **Los cambios se pierden al reiniciar la API** y cada réplica tendría su propio estado. Los PUT concurrentes se serializan: la última escritura válida prevalece, sin control de versión del cliente. Las listas devuelven todo el catálogo con orden estable; no se implementa paginación para este alcance.

El enunciado permite memoria, por lo que no se añade PostgreSQL ni caché. Si se necesita durabilidad, varias réplicas o un catálogo grande, la siguiente implementación será PostgreSQL, con repositorios y consultas específicas, restricciones de unicidad y claves foráneas, transacciones y control de versión. El puerto de callbacks actual requeriría evolucionar para no cargar todo el catálogo en SQL. El dominio, los contratos HTTP y los casos de uso proporcionan fronteras para esa evolución; no se afirma que baste con cambiar una cadena de conexión. Valkey solo se incorporaría con un caso de uso medido de caché.

«Mis Pokémon» representa una única colección de demostración: el ejercicio no solicita cuentas ni autenticación. Las operaciones heredan trazas, logs y métricas del comportamiento de MediatR, visibles en Aspire.

## Datos iniciales y fuentes

Se cargan cinco especies, cinco ejemplares de nivel 20 y 21 movimientos al iniciar el proceso. Las cinco especies tienen un único tipo en su forma normal. Sus estadísticas son las documentadas en los ejemplos del ejercicio 1. La salud inicial igual a la estadística base es una simplificación; no se calcula crecimiento por nivel.

| Especie | Sufijo de ID de especie | Sufijo de ID de ejemplar | Fuente del aprendizaje |
| --- | --- | --- | --- |
| Charmander | 101 | 201 | [Scarlet/Violet](https://pokemondb.net/pokedex/charmander/moves/9) |
| Squirtle | 102 | 202 | [Scarlet/Violet](https://pokemondb.net/pokedex/squirtle/moves/9) |
| Pikachu | 103 | 203 | [Scarlet/Violet](https://pokemondb.net/pokedex/pikachu/moves/9) |
| Sandshrew | 104 | 204 | [Scarlet/Violet, forma normal](https://pokemondb.net/pokedex/sandshrew/moves/9) |
| Eevee | 105 | 205 | [Scarlet/Violet](https://pokemondb.net/pokedex/eevee/moves/9) |

Se incluye un subconjunto de movimientos aprendidos por nivel, no el catálogo oficial completo. Los niveles y las potencias de los ataques seleccionados proceden de esas tablas. Se omiten efectos secundarios, prioridad y mecánicas de acumulación de daño. Los datos están versionados en PokedexSeed.cs y no requieren conexiones externas al ejecutar ni probar la solución. Los seis escenarios del ejercicio 1 siguen siendo ejemplos independientes del catálogo persistido en memoria.

## Verificación

Las pruebas de Pokédex cubren los tres CRUD, Location, campos obligatorios, proyecciones, cuatro movimientos, incompatibilidad y nivel, referencias, edición compartida, rollback, cancelación, aislamiento de colecciones, altas concurrentes, ejemplos OpenAPI ejecutables y uso de un ejemplar en POST /damage sin modificar su salud. Se ejecutan junto con las pruebas del ejercicio 1.

El ejercicio 3 sigue pendiente. Este cambio se desarrolla en feature/pokedex; no se integra automáticamente en develop ni main.

Verificación de esta entrega: 513 pruebas superadas en Windows y en Linux durante la construcción Docker. El script de CRUD se ejecutó contra la imagen arrancada y eliminó sus datos temporales. Scalar respondió HTTP 200. El workflow remoto de GitHub aún no se ha ejecutado.
