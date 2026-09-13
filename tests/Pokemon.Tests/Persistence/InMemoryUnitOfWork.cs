using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Pokedex;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Tests.Persistence;

/// <summary>Doble transaccional de pruebas: copia agregados inmutables y publica solo al confirmar.</summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork, IReadSession, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot state = new();
    /// <summary>Inicializa la unidad de trabajo de prueba y carga opcionalmente el catálogo inicial.</summary>
    public InMemoryUnitOfWork(bool seed = true)
    {
        if (seed) PokedexSeed.PopulateAsync(state, default).GetAwaiter().GetResult();
    }
    /// <summary>Ejecuta una consulta sobre una copia coherente del estado de prueba sin publicar modificaciones.</summary>
    public async Task<T> ReadAsync<T>(Func<IReadRepositoryScope, Task<T>> query, CancellationToken token)
    {
        await gate.WaitAsync(token);
        var snapshot = state.Copy(readOnly: true);
        try { token.ThrowIfCancellationRequested(); return await query(snapshot); }
        finally { snapshot.Close(); gate.Release(); }
    }
    /// <summary>Ejecuta un comando sobre una copia y publica su estado solo si termina sin errores ni cancelación.</summary>
    public async Task<T> WriteAsync<T>(Func<IRepositoryScope, Task<T>> command, CancellationToken token)
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
    /// <summary>Libera el semáforo que coordina las operaciones de la unidad de trabajo de prueba.</summary>
    public void Dispose() => gate.Dispose();
    private sealed class Snapshot(bool readOnly = false) : IRepositoryScope
    {
        private bool closed;
        private InMemoryMoveRepository? moves;
        private InMemorySpeciesRepository? species;
        private InMemoryOwnedPokemonRepository? pokemon;
        private SessionBattleRepository? battles;
        /// <summary>Rechaza el acceso a una copia transaccional de prueba que ya está cerrada.</summary>
        private void Ensure() => ObjectDisposedException.ThrowIf(closed, this);
        /// <summary>Marca la copia transaccional de prueba como cerrada.</summary>
        public void Close() => closed = true;
        /// <summary>Obtiene un lector de la copia transaccional utilizada por la prueba.</summary>
        public TReader GetReader<TReader>() where TReader : class, IReadRepository => Resolve<TReader>();
        /// <summary>Obtiene un escritor de prueba y rechaza su resolución en una copia de solo lectura.</summary>
        public TRepository GetRepository<TRepository>() where TRepository : class, IWriteRepository
        {
            if (readOnly) throw new InvalidOperationException("A read session cannot resolve write repositories.");
            return Resolve<TRepository>();
        }
        /// <summary>Crea o reutiliza el doble de repositorio correspondiente al contrato solicitado.</summary>
        private T Resolve<T>() where T : class
        {
            Ensure();
            object value = typeof(T).Name switch
            {
                nameof(IMoveReader) or nameof(IMoveRepository) => moves ??= new(Ensure),
                nameof(ISpeciesReader) or nameof(ISpeciesRepository) => species ??= new(Ensure),
                nameof(IOwnedPokemonReader) or nameof(IOwnedPokemonRepository) => pokemon ??= new(Ensure),
                nameof(IBattleReader) or nameof(IBattleRepository) => battles ??= new(Ensure),
                _ => throw new InvalidOperationException("Unknown repository contract.")
            };
            return (T)value;
        }
        /// <summary>Copia los agregados inmutables a un nuevo estado transaccional independiente.</summary>
        public Snapshot Copy(bool readOnly = false)
        {
            var copy = new Snapshot(readOnly);
            if (moves is not null) { copy.moves = new(copy.Ensure); foreach (var pair in moves.Entries) copy.moves.Entries.Add(pair.Key, pair.Value); }
            if (species is not null) { copy.species = new(copy.Ensure); foreach (var pair in species.Entries) copy.species.Entries.Add(pair.Key, pair.Value); }
            if (pokemon is not null) { copy.pokemon = new(copy.Ensure); foreach (var pair in pokemon.Entries) copy.pokemon.Entries.Add(pair.Key, pair.Value); }
            if (battles is not null) { copy.battles = new(copy.Ensure); foreach (var pair in battles.Entries) copy.battles.Entries.Add(pair.Key, pair.Value); }
            return copy;
        }
    }

    /// <summary>Gestiona partidas dentro de la copia transaccional de estado utilizada por las pruebas.</summary>
    private sealed class SessionBattleRepository(Action ensure) : IBattleReader, IBattleRepository
    {
        internal readonly Dictionary<Guid, BattleAggregate> Entries = new();

        /// <summary>Añade una partida completa y rechaza un identificador que ya existe.</summary>
        public Task Add(BattleAggregate battle, CancellationToken token)
        {
            ensure(); token.ThrowIfCancellationRequested();
            if (!Entries.TryAdd(battle.Id, battle)) throw new BattleConflictException("Battle identity already exists.");
            return Task.CompletedTask;
        }

        /// <summary>Recupera una partida por su identificador o informa de que no existe.</summary>
        public Task<BattleAggregate> Get(Guid id, CancellationToken token)
        {
            ensure(); token.ThrowIfCancellationRequested();
            return Task.FromResult(Entries.TryGetValue(id, out var battle) ? battle : throw new BattleNotFoundException("Battle was not found."));
        }

        /// <summary>Aplica una acción de forma atómica y exige avanzar una única versión de la misma partida.</summary>
        public async Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token)
        {
            var original = await Get(id, token);
            var updated = action(original);
            if (updated.Id != id || updated.Version != original.Version + 1) throw new InvalidOperationException("A turn must advance one version.");
            token.ThrowIfCancellationRequested();
            Entries[id] = updated;
            return updated;
        }
    }
}
