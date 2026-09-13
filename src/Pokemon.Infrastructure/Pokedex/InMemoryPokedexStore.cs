using Pokemon.Application.Feature.Pokedex.Persistence;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Infrastructure.Pokedex;

/// <summary>
/// Adaptador de un único proceso. Serializa operaciones para proteger relaciones y unicidad.
/// Trabaja en una copia y solo la publica al terminar; una excepción revierte todo el comando.
/// No persiste entre reinicios ni sirve como almacenamiento compartido entre réplicas.
/// </summary>
public sealed class InMemoryPokedexStore : IPokedexStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot state = new();

    public InMemoryPokedexStore(bool seed = true)
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
        private readonly Dictionary<Guid, Species> species = [];
        private readonly Dictionary<Guid, CatalogMove> moves = [];
        private readonly Dictionary<Guid, OwnedPokemon> pokemon = [];
        public IReadOnlyCollection<Species> Species => species.Values.ToArray();
        public IReadOnlyCollection<CatalogMove> Moves => moves.Values.ToArray();
        public IReadOnlyCollection<OwnedPokemon> Pokemon => pokemon.Values.ToArray();
        public void Save(Species value) => species[value.Id] = value;
        public void Save(CatalogMove value) => moves[value.Id] = value;
        public void Save(OwnedPokemon value) => pokemon[value.Id] = value;
        public void DeleteSpecies(Guid id) => species.Remove(id);
        public void DeleteMove(Guid id) => moves.Remove(id);
        public void DeletePokemon(Guid id) => pokemon.Remove(id);
        public Snapshot Copy()
        {
            var copy = new Snapshot();
            foreach (var value in species.Values) copy.Save(value);
            foreach (var value in moves.Values) copy.Save(value);
            foreach (var value in pokemon.Values) copy.Save(value);
            return copy;
        }
    }
}
