using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>
/// Crea y reutiliza bajo demanda una instancia de cada repositorio durante una operación transaccional.
/// </summary>
internal sealed class RepositoryScope(DatabaseSession database, RepositoryRegistry registry, bool readOnly) : IRepositoryScope
{
    private readonly Dictionary<Type, object> instances = new();
    private CatalogDatabaseSession? catalog;
    internal DatabaseSession Database => database;
    internal CatalogDatabaseSession Catalog => catalog ??= new(database, readOnly);

    /// <summary>
    /// Obtiene el lector solicitado y reutiliza su instancia durante la operación actual.
    /// </summary>
    public TReader GetReader<TReader>() where TReader : class, IReadRepository => Resolve<TReader>();

    /// <summary>
    /// Obtiene el repositorio de escritura solicitado y rechaza su resolución en una sesión de lectura.
    /// </summary>
    public TRepository GetRepository<TRepository>() where TRepository : class, IWriteRepository
    {
        if (readOnly) throw new InvalidOperationException("A read session cannot resolve write repositories.");
        return Resolve<TRepository>();
    }

    /// <summary>
    /// Resuelve el contrato registrado y crea su implementación únicamente en el primer acceso.
    /// </summary>
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
