using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence.Repositories;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Mantiene las fábricas que permiten añadir repositorios sin modificar la unidad de trabajo.</summary>
internal sealed class RepositoryRegistry
{
    /// <summary>Asocia el tipo de implementación con la fábrica que crea el repositorio dentro de una operación.</summary>
    internal sealed record Registration(Type Implementation, Func<RepositoryScope, object> Create);
    private readonly Dictionary<Type, Registration> entries = new();

    /// <summary>Asocia una fábrica a sus contratos de repositorio y devuelve el registro para encadenar configuraciones.</summary>
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

    /// <summary>Obtiene la fábrica asociada al contrato o informa de que no está registrado.</summary>
    public Registration Find(Type contract) => entries.TryGetValue(contract, out var registration)
        ? registration : throw new InvalidOperationException($"Repository contract {contract.Name} is not registered.");

    /// <summary>Configura las fábricas de los repositorios de la aplicación sin crear sus instancias.</summary>
    public static RepositoryRegistry CreateDefault() => new RepositoryRegistry()
        .Register(scope => new MoveRepository(scope.Catalog), typeof(IMoveReader), typeof(IMoveRepository))
        .Register(scope => new SpeciesRepository(scope.Catalog), typeof(ISpeciesReader), typeof(ISpeciesRepository))
        .Register(scope => new OwnedPokemonRepository(scope.Catalog, scope.GetReader<ISpeciesReader>()), typeof(IOwnedPokemonReader), typeof(IOwnedPokemonRepository))
        .Register(scope => new BattleRepository(scope.Database), typeof(IBattleReader), typeof(IBattleRepository));
}
