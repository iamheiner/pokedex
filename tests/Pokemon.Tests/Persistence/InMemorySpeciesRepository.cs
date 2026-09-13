using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>Repositorio privado de un snapshot; la unidad de trabajo controla su publicación.</summary>
internal sealed class InMemorySpeciesRepository : ISpeciesRepository
{
    private readonly Dictionary<Guid, Species> entries = [];
    public IReadOnlyCollection<Species> List() => entries.Values.ToArray();
    public Species? Find(Guid id) => entries.GetValueOrDefault(id);
    public void Save(Species aggregate) => entries[aggregate.Id] = aggregate;
    public void Delete(Guid id) => entries.Remove(id);
}
