using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>Repositorio privado de un snapshot; la unidad de trabajo controla su publicación.</summary>
internal sealed class InMemoryMoveRepository : IMoveRepository
{
    private readonly Dictionary<Guid, CatalogMove> entries = [];
    public IReadOnlyCollection<CatalogMove> List() => entries.Values.ToArray();
    public CatalogMove? Find(Guid id) => entries.GetValueOrDefault(id);
    public void Save(CatalogMove aggregate) => entries[aggregate.Id] = aggregate;
    public void Delete(Guid id) => entries.Remove(id);
}
