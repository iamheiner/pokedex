using Pokemon.Infrastructure.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Tests.Persistence;

/// <summary>
/// Adaptador de un único proceso. Serializa operaciones para proteger relaciones y unicidad.
/// Trabaja en una copia y solo la publica al terminar; una excepción revierte todo el comando.
/// No persiste entre reinicios ni sirve como almacenamiento compartido entre réplicas.
/// </summary>
public sealed class InMemoryPokedexUnitOfWork : IPokedexUnitOfWork, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot state = new();

    public InMemoryPokedexUnitOfWork(bool seed = true)
    {
        if (seed) PokedexSeed.Populate(state);
    }

    public async Task<T> Read<T>(Func<IPokedexReader, T> query, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { token.ThrowIfCancellationRequested(); return query(state.Copy()); }
        finally { gate.Release(); }
    }

    public async Task<T> Write<T>(Func<IPokedexSession, T> command, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var transaction = state.Copy();
            var result = command(transaction);
            token.ThrowIfCancellationRequested();
            // Otra copia impide que un callback retenido pueda mutar el estado publicado.
            state = transaction.Copy();
            return result;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();

    private sealed class Snapshot : IPokedexSession
    {
        public ISpeciesRepository SpeciesRepository { get; } = new InMemorySpeciesRepository();
        public IMoveRepository MoveRepository { get; } = new InMemoryMoveRepository();
        public IOwnedPokemonRepository PokemonRepository { get; } = new InMemoryOwnedPokemonRepository();
        public IReadOnlyCollection<Species> Species => SpeciesRepository.List();
        public IReadOnlyCollection<CatalogMove> Moves => MoveRepository.List();
        public IReadOnlyCollection<OwnedPokemon> Pokemon => PokemonRepository.List();
        public Snapshot Copy()
        {
            var copy = new Snapshot();
            foreach (var value in Species) copy.SpeciesRepository.Save(value);
            foreach (var value in Moves) copy.MoveRepository.Save(value);
            foreach (var value in Pokemon) copy.PokemonRepository.Save(value);
            return copy;
        }
    }
}
