using Pokemon.Domain.Pokedex;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex;

internal static class PokedexMapping
{
    public static Species Species(IPokedexReader data, Guid id) => data.Species.SingleOrDefault(s => s.Id == id)
        ?? throw new PokedexNotFoundException("Species was not found.");
    public static CatalogMove Move(IPokedexReader data, Guid id) => data.Moves.SingleOrDefault(m => m.Id == id)
        ?? throw new PokedexNotFoundException("Move was not found.");
    public static OwnedPokemon Pokemon(IPokedexReader data, Guid id) => data.Pokemon.SingleOrDefault(p => p.Id == id)
        ?? throw new PokedexNotFoundException("Pokemon was not found.");
    public static MoveView View(CatalogMove move) => new(move.Id, move.Name, move.Power, move.Type);
    public static StatsInput View(BaseStats stats) => new(stats.Health, stats.Attack, stats.Defense, stats.SpecialAttack, stats.SpecialDefense, stats.Speed);
    public static IReadOnlyList<LearningView> Learnset(IPokedexReader data, Species species) => species.Learnset
        .OrderBy(e => e.Level).ThenBy(e => e.MoveId).Select(e => new LearningView(View(Move(data, e.MoveId)), e.Level)).ToArray();
    public static SpeciesView View(IPokedexReader data, Species species) => new(species.Id, species.Name, species.Type, View(species.Stats), Learnset(data, species));
    public static PokemonView View(IPokedexReader data, OwnedPokemon pokemon)
    {
        var species = Species(data, pokemon.SpeciesId);
        return new(pokemon.Id, species.Id, species.Name, pokemon.Name, species.Type, pokemon.Level,
            pokemon.CurrentHealth, pokemon.TotalHealth, View(species.Stats), pokemon.MoveIds.Select(id => View(Move(data, id))).ToArray());
    }
}
