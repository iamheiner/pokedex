using Pokemon.Domain.Pokedex;
namespace Pokemon.Application.Feature.Pokedex.Persistence;

/// <summary>Lecturas coherentes sin permitir que una Query modifique el almacén.</summary>
public interface IPokedexReader
{
    IReadOnlyCollection<Species> Species { get; }
    IReadOnlyCollection<CatalogMove> Moves { get; }
    IReadOnlyCollection<OwnedPokemon> Pokemon { get; }
}

/// <summary>Operaciones sobre agregados dentro de una única transacción.</summary>
public interface IPokedexSession : IPokedexReader
{
    void Save(Species species);
    void Save(CatalogMove move);
    void Save(OwnedPokemon pokemon);
    void DeleteSpecies(Guid id);
    void DeleteMove(Guid id);
    void DeletePokemon(Guid id);
}

/// <summary>
/// Puerto de persistencia de Application. Write publica todos los cambios juntos o ninguno.
/// El adaptador actual usa memoria; un adaptador SQL deberá conservar esta garantía.
/// Los callbacks son síncronos porque no realizan E/S; el puerto permite esperar el acceso.
/// </summary>
public interface IPokedexStore
{
    Task<T> Read<T>(Func<IPokedexReader, T> query, CancellationToken token);
    Task<T> Write<T>(Func<IPokedexSession, T> command, CancellationToken token);
}
