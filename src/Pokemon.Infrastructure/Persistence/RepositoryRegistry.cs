using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence.Repositories;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Registro de fábricas de repositorios. Añadir un agregado no modifica la unidad de trabajo.</summary>
internal sealed class RepositoryRegistry
{
    internal sealed record Registration(Type Implementation, Func<RepositoryScope, object> Create);
    private readonly Dictionary<Type, Registration> entries = new();
    public RepositoryRegistry Register<T>(Func<RepositoryScope, T> create, params Type[] contracts) where T : class
    {
        var registration = new Registration(typeof(T), scope => create(scope));
        foreach (var contract in contracts)
        {
            if (!contract.IsInterface || !contract.IsAssignableFrom(typeof(T)) ||
                (!typeof(IReadRepository).IsAssignableFrom(contract) && !typeof(IWriteRepository).IsAssignableFrom(contract)))
                throw new ArgumentException("Only implemented repository contracts can be registered.", nameof(contracts));
            entries.Add(contract, registration);
        }
        return this;
    }
    public Registration Find(Type contract) => entries.TryGetValue(contract, out var registration)
        ? registration : throw new InvalidOperationException($"Repository contract {contract.Name} is not registered.");
    public static RepositoryRegistry CreateDefault() => new RepositoryRegistry()
        .Register(scope => new MoveRepository(scope.Catalog), typeof(IMoveReader), typeof(IMoveRepository))
        .Register(scope => new SpeciesRepository(scope.Catalog), typeof(ISpeciesReader), typeof(ISpeciesRepository))
        .Register(scope => new OwnedPokemonRepository(scope.Catalog, scope.GetReader<ISpeciesReader>()), typeof(IOwnedPokemonReader), typeof(IOwnedPokemonRepository))
        .Register(scope => new BattleRepository(scope.Database), typeof(IBattleReader), typeof(IBattleRepository));
}
