using Pokemon.Application.Feature.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Pokedex;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex;

/// <summary>
/// Proyecta agregados a respuestas. Las relaciones se resuelven en lotes, no con una consulta por elemento.
/// </summary>
internal static class PokedexMapping
{
    /// <summary>
    /// Recupera una especie o informa de que no existe para el caso de uso solicitado.
    /// </summary>
    public static async Task<Species> SpeciesAsync(IReadRepositoryScope data, Guid id, CancellationToken token) =>
        await data.GetReader<ISpeciesReader>().FindAsync(id, token) ?? throw new PokedexNotFoundException("Species was not found.");
    /// <summary>
    /// Recupera un movimiento o informa de que no existe para el caso de uso solicitado.
    /// </summary>
    public static async Task<CatalogMove> MoveAsync(IReadRepositoryScope data, Guid id, CancellationToken token) =>
        await data.GetReader<IMoveReader>().FindAsync(id, token) ?? throw new PokedexNotFoundException("Move was not found.");
    /// <summary>
    /// Recupera un ejemplar o informa de que no existe para el caso de uso solicitado.
    /// </summary>
    public static async Task<OwnedPokemon> PokemonAsync(IReadRepositoryScope data, Guid id, CancellationToken token) =>
        await data.GetReader<IOwnedPokemonReader>().FindAsync(id, token) ?? throw new PokedexNotFoundException("Pokemon was not found.");
    /// <summary>
    /// Proyecta los datos del movimiento a su contrato de salida.
    /// </summary>
    public static MoveView View(CatalogMove move) => new(move.Id, move.Name, move.Power, move.Type);
    /// <summary>
    /// Proyecta las seis estadísticas base a su contrato de salida.
    /// </summary>
    public static StatsView View(BaseStats stats) => new(stats.Health, stats.Attack, stats.Defense, stats.SpecialAttack, stats.SpecialDefense, stats.Speed);
    /// <summary>
    /// Proyecta el plan de aprendizaje ordenado por nivel e identificador de movimiento.
    /// </summary>
    private static IReadOnlyList<LearningView> Learnset(Species species, IReadOnlyDictionary<Guid, CatalogMove> moves) =>
        species.Learnset.OrderBy(entry => entry.Level).ThenBy(entry => entry.MoveId)
            .Select(entry => new LearningView(View(moves[entry.MoveId]), entry.Level)).ToArray();
    /// <summary>
    /// Proyecta una colección de especies resolviendo sus movimientos en un lote.
    /// </summary>
    public static async Task<IReadOnlyList<SpeciesView>> ViewsAsync(IReadRepositoryScope data, IReadOnlyList<Species> species, CancellationToken token)
    {
        var ids = species.SelectMany(value => value.Learnset).Select(entry => entry.MoveId).Distinct().ToArray();
        var moves = (await data.GetReader<IMoveReader>().FindManyAsync(ids, token)).ToDictionary(value => value.Id);
        return species.Select(value => new SpeciesView(value.Id, value.Name, value.Type, View(value.Stats), Learnset(value, moves))).ToArray();
    }
    /// <summary>
    /// Proyecta una colección de ejemplares resolviendo sus especies y movimientos en lotes.
    /// </summary>
    public static async Task<IReadOnlyList<PokemonView>> ViewsAsync(IReadRepositoryScope data, IReadOnlyList<OwnedPokemon> pokemon, CancellationToken token)
    {
        var species = (await data.GetReader<ISpeciesReader>().FindManyAsync(pokemon.Select(value => value.SpeciesId).Distinct().ToArray(), token)).ToDictionary(value => value.Id);
        var moves = (await data.GetReader<IMoveReader>().FindManyAsync(pokemon.SelectMany(value => value.MoveIds).Distinct().ToArray(), token)).ToDictionary(value => value.Id);
        return pokemon.Select(value =>
        {
            var definition = species[value.SpeciesId];
            return new PokemonView(value.Id, definition.Id, definition.Name, value.Name, definition.Type, value.Level,
                value.CurrentHealth, value.TotalHealth, View(definition.Stats), value.MoveIds.Select(id => View(moves[id])).ToArray());
        }).ToArray();
    }
    /// <summary>
    /// Construye la respuesta de una especie junto con su plan de aprendizaje.
    /// </summary>
    public static async Task<SpeciesView> ViewAsync(IReadRepositoryScope data, Species species, CancellationToken token) =>
        (await ViewsAsync(data, new[] { species }, token)).Single();
    /// <summary>
    /// Construye la respuesta de un ejemplar junto con sus estadísticas y movimientos aprendidos.
    /// </summary>
    public static async Task<PokemonView> ViewAsync(IReadRepositoryScope data, OwnedPokemon pokemon, CancellationToken token) =>
        (await ViewsAsync(data, new[] { pokemon }, token)).Single();
}
