using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Pokedex;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands;

/// <summary>Obtiene los hechos necesarios para construir agregados; el dominio protege las invariantes.</summary>
internal static class PokedexChanges
{
    public static async Task<CatalogMove> MoveAsync(IReadRepositoryScope data, Guid id, MoveInput input, CancellationToken token)
    {
        if (input is null) throw new PokedexRuleException("Move data is required.");
        var move = new CatalogMove(id, input.Name, input.Power, input.Type);
        if (await data.GetReader<IMoveReader>().NameExistsAsync(move.Name, id, token))
            throw new PokedexConflictException("A catalog entry with this name already exists.");
        return move;
    }
    public static async Task<Species> SpeciesAsync(IReadRepositoryScope data, Guid id, SpeciesInput input, CancellationToken token)
    {
        if (input?.Stats is null || input.Learnset is null || input.Learnset.Any(entry => entry is null))
            throw new PokedexRuleException("Stats and non-null learnset entries are required.");
        var species = new Species(id, input.Name, input.Type, input.Stats.ToDomain(), input.Learnset.Select(entry => new LearnableMove(entry.MoveId, entry.Level)));
        if (await data.GetReader<ISpeciesReader>().NameExistsAsync(species.Name, id, token))
            throw new PokedexConflictException("A catalog entry with this name already exists.");
        var referenced = await data.GetReader<IMoveReader>().FindManyAsync(species.Learnset.Select(entry => entry.MoveId).ToArray(), token);
        if (referenced.Count != species.Learnset.Count)
            throw new PokedexRuleException("Learnset refers to a move that does not exist.");
        LearningPolicy.EnsureCompatible(species, await data.GetReader<IOwnedPokemonReader>().FindBySpeciesAsync(id, token));
        return species;
    }
    public static async Task<OwnedPokemon> PokemonAsync(IReadRepositoryScope data, Guid id, PokemonInput input, CancellationToken token)
    {
        if (input is null) throw new PokedexRuleException("Pokemon data is required.");
        var species = await data.GetReader<ISpeciesReader>().FindAsync(input.SpeciesId, token)
            ?? throw new PokedexRuleException("Referenced species does not exist.");
        return new OwnedPokemon(id, species, input.Name, input.Level, input.CurrentHealth, input.TotalHealth, input.MoveIds);
    }
}
