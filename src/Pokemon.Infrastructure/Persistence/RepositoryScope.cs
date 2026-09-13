using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Una instancia de repositorio por implementación y operación; creación diferida al primer acceso.</summary>
internal sealed class RepositoryScope(DatabaseSession database, RepositoryRegistry registry, bool readOnly) : IRepositoryScope
{
    private readonly Dictionary<Type, object> instances = new();
    private CatalogDatabaseSession? catalog;
    internal DatabaseSession Database => database;
    internal CatalogDatabaseSession Catalog => catalog ??= new(database, readOnly);
    public TReader GetReader<TReader>() where TReader : class, IReadRepository => Resolve<TReader>();
    public TRepository GetRepository<TRepository>() where TRepository : class, IWriteRepository
    {
        if (readOnly) throw new InvalidOperationException("A read session cannot resolve write repositories.");
        return Resolve<TRepository>();
    }
    private T Resolve<T>() where T : class
    {
        database.EnsureActive();
        var registration = registry.Find(typeof(T));
        if (!instances.TryGetValue(registration.Implementation, out var instance))
        {
            instance = registration.Create(this);
            instances.Add(registration.Implementation, instance);
        }
        return (T)instance;
    }
}
