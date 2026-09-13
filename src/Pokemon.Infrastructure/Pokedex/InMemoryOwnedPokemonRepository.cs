using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex;

/// <summary>Repositorio privado de un snapshot; la unidad de trabajo controla su publicación.</summary>
internal sealed class InMemoryOwnedPokemonRepository : IOwnedPokemonRepository
{
    private readonly Dictionary<Guid, OwnedPokemon> entries = [];
    public IReadOnlyCollection<OwnedPokemon> List() => entries.Values.ToArray();
    public OwnedPokemon? Find(Guid id) => entries.GetValueOrDefault(id);
    public void Save(OwnedPokemon aggregate) => entries[aggregate.Id] = aggregate;
    public void Delete(Guid id) => entries.Remove(id);
}
