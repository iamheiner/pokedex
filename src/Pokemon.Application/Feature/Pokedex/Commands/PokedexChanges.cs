using Pokemon.Domain.Pokedex;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands;

internal static class PokedexChanges
{
    public static CatalogMove Move(IPokedexReader data, Guid id, MoveInput input)
    {
        if (input is null) throw new PokedexRuleException("Move data is required.");
        var move = new CatalogMove(id, input.Name, input.Power, input.Type);
        Unique(data.Moves.Where(m => m.Id != id).Select(m => m.Name), move.Name);
        return move;
    }
    public static Species Species(IPokedexReader data, Guid id, SpeciesInput input)
    {
        if (input?.Stats is null || input.Learnset is null || input.Learnset.Any(e => e is null))
            throw new PokedexRuleException("Stats and non-null learnset entries are required.");
        var species = new Species(id, input.Name, input.Type, input.Stats.ToDomain(), input.Learnset.Select(e => new LearnableMove(e.MoveId, e.Level)));
        Unique(data.Species.Where(s => s.Id != id).Select(s => s.Name), species.Name);
        if (species.Learnset.Any(e => !data.Moves.Any(m => m.Id == e.MoveId)))
            throw new PokedexRuleException("Learnset refers to a move that does not exist.");
        // Cambiar el plan de aprendizaje no puede invalidar ejemplares ya registrados.
        if (data.Pokemon.Where(p => p.SpeciesId == id).Any(p => p.MoveIds.Any(m => !species.CanLearn(m, p.Level))))
            throw new PokedexConflictException("The new learnset would invalidate an existing Pokemon.");
        return species;
    }
    public static OwnedPokemon Pokemon(IPokedexReader data, Guid id, PokemonInput input)
    {
        if (input is null) throw new PokedexRuleException("Pokemon data is required.");
        var species = data.Species.SingleOrDefault(s => s.Id == input.SpeciesId)
            ?? throw new PokedexRuleException("Referenced species does not exist.");
        return new OwnedPokemon(id, species, input.Name, input.Level, input.CurrentHealth, input.TotalHealth, input.MoveIds);
    }
    private static void Unique(IEnumerable<string> existing, string name)
    {
        if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new PokedexConflictException("A catalog entry with this name already exists.");
    }
}
