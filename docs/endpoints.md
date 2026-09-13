# Organización de endpoints

Cada operación de negocio tiene una clase y un fichero propios dentro de su Feature. Los archivos agrupadores registran las operaciones y la configuración común del recurso.

```text
Pokemon.Api/Feature/Pokedex/Moves/
  MovesEndpoints.cs
  Commands/
    CreateMoveEndpoint.cs
    UpdateMoveEndpoint.cs
    DeleteMoveEndpoint.cs
  Queries/
    ListMovesEndpoint.cs
    GetMoveEndpoint.cs
    GetPokemonSharingMoveEndpoint.cs
    GetSpeciesSharingMoveEndpoint.cs
```

Cada clase de operación contiene dos métodos, separados y documentados con un resumen XML:

- `Map`: registra la ruta, las respuestas HTTP, el resumen, la descripción y los ejemplos de OpenAPI que correspondan.
- `HandleAsync`: recibe los datos HTTP, envía el Command o Query mediante `ISender` y construye la respuesta HTTP.

Los Commands y Queries de negocio y sus handlers permanecen en Application. La API adapta el transporte; Application coordina los casos de uso y el dominio protege sus reglas. Por ejemplo, `CreateMoveEndpoint` envía `CreateMoveCommand` y devuelve `201 Created` con la cabecera `Location`.

La clasificación depende del efecto de la operación: crear, actualizar, eliminar o jugar un turno son comandos; recuperar datos y calcular daño sin modificar salud son consultas. Por eso `CalculateDamageEndpoint` pertenece a `Queries`, aunque reciba un cuerpo mediante `POST /damage`.

Las sondas de salud también tienen archivos separados. La disponibilidad utiliza el mecanismo de health checks de ASP.NET Core. OpenAPI y Scalar conservan su registro conjunto bajo la política de autenticación documental; sus rutas las generan sus bibliotecas. Estos servicios técnicos no necesitan Commands o Queries de negocio en Application.

La reorganización conserva las URL, los contratos JSON, los códigos HTTP y las políticas de autorización. Las pruebas HTTP existentes cubren su comportamiento; una comprobación adicional exige resumen y descripción para las 23 operaciones de negocio publicadas en OpenAPI.
