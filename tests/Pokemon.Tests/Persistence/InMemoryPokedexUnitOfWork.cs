using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Pokedex;
namespace Pokemon.Tests.Persistence;

/// <summary>Doble transaccional exclusivamente de pruebas. Nunca forma parte del ensamblado desplegado.</summary>
public sealed class InMemoryPokedexUnitOfWork : IPokedexReadSession, IPokedexUnitOfWork, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot state = new();
    public InMemoryPokedexUnitOfWork(bool seed = true)
    {
        if (seed) PokedexSeed.PopulateAsync(state, default).GetAwaiter().GetResult();
    }
    public async Task<T> ReadAsync<T>(Func<IPokedexReader, Task<T>> query, CancellationToken token)
    {
        await gate.WaitAsync(token);
        var snapshot = state.Copy();
        try { token.ThrowIfCancellationRequested(); return await query(snapshot); }
        finally { snapshot.Close(); gate.Release(); }
    }
    public async Task<T> WriteAsync<T>(Func<IPokedexSession, Task<T>> command, CancellationToken token)
    {
        await gate.WaitAsync(token);
        var snapshot = state.Copy();
        try
        {
            token.ThrowIfCancellationRequested();
            var result = await command(snapshot);
            token.ThrowIfCancellationRequested();
            state = snapshot.Copy();
            return result;
        }
        finally { snapshot.Close(); gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private sealed class Snapshot : IPokedexSession
    {
        private bool closed;
        private readonly InMemoryMoveRepository moves;
        private readonly InMemorySpeciesRepository species;
        private readonly InMemoryOwnedPokemonRepository pokemon;
        public Snapshot()
        {
            moves = new(Ensure); species = new(Ensure); pokemon = new(Ensure);
        }
        private void Ensure() => ObjectDisposedException.ThrowIf(closed, this);
        public void Close() => closed = true;
        public ISpeciesRepository SpeciesRepository => species;
        public IMoveRepository MoveRepository => moves;
        public IOwnedPokemonRepository PokemonRepository => pokemon;
        public ISpeciesReader Species => species;
        public IMoveReader Moves => moves;
        public IOwnedPokemonReader Pokemon => pokemon;
        public Snapshot Copy()
        {
            var copy = new Snapshot();
            foreach (var (id, value) in moves.Entries) copy.moves.Entries.Add(id, value);
            foreach (var (id, value) in species.Entries) copy.species.Entries.Add(id, value);
            foreach (var (id, value) in pokemon.Entries) copy.pokemon.Entries.Add(id, value);
            return copy;
        }
    }
}
