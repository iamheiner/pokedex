namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Snapshot coherente de lectura para las consultas y reglas entre agregados.</summary>
public interface IPokedexReader
{
    IReadOnlyCollection<Species> Species { get; }
    IReadOnlyCollection<CatalogMove> Moves { get; }
    IReadOnlyCollection<OwnedPokemon> Pokemon { get; }
}

/// <summary>Repositorios que comparten una misma transacción y snapshot.</summary>
public interface IPokedexSession : IPokedexReader
{
    ISpeciesRepository SpeciesRepository { get; }
    IMoveRepository MoveRepository { get; }
    IOwnedPokemonRepository PokemonRepository { get; }
}

/// <summary>
/// Delimita la unidad de trabajo entre los tres agregados. Write confirma todos los
/// repositorios juntos o revierte todos. Los callbacks trabajan sobre el snapshot ya
/// cargado y no hacen E/S; el adaptador realiza la E/S y la sincronización fuera de ellos.
/// </summary>
public interface IPokedexUnitOfWork
{
    Task<T> Read<T>(Func<IPokedexReader, T> query, CancellationToken token);
    Task<T> Write<T>(Func<IPokedexSession, T> command, CancellationToken token);
}
