using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Persistence.Repositories;

/// <summary>Consulta y persiste ejemplares y sus movimientos aprendidos en la transacción de la unidad de trabajo.</summary>
internal sealed class OwnedPokemonRepository(CatalogDatabaseSession session, ISpeciesReader species) : IOwnedPokemonRepository, IOwnedPokemonReader
{
    /// <summary>Representa la identidad, especie, nivel y salud almacenados de un ejemplar.</summary>
    private sealed record Row(Guid Id, Guid SpeciesId, string Name, int Level, int CurrentHealth, int TotalHealth);

    /// <summary>Relaciona un ejemplar con uno de sus movimientos aprendidos.</summary>
    private sealed record LearnedRow(Guid PokemonId, Guid MoveId);

    /// <summary>Reconstruye los ejemplares seleccionados con sus especies y movimientos cargados por lotes.</summary>
    private async Task<IReadOnlyList<OwnedPokemon>> Select(string filter, object args, CancellationToken token)
    {
        var rows = (await session.QueryAsync<Row>("""
            SELECT id AS Id,species_id AS SpeciesId,name AS Name,level AS Level,
            current_health AS CurrentHealth,total_health AS TotalHealth FROM pokedex_pokemon
            """ + " " + filter, args, token)).ToArray();
        if (rows.Length == 0) return [];
        var definitions = (await species.FindManyAsync(rows.Select(row => row.SpeciesId).Distinct().ToArray(), token)).ToDictionary(value => value.Id);
        var learned = (await session.QueryAsync<LearnedRow>("""
            SELECT pokemon_id AS PokemonId,move_id AS MoveId FROM pokedex_learned_moves
            WHERE pokemon_id=ANY(@Ids) ORDER BY slot
            """, new { Ids = rows.Select(row => row.Id).ToArray() }, token)).ToLookup(row => row.PokemonId);
        return DomainMaterializer.Create(() => rows.Select(row => new OwnedPokemon(row.Id, definitions[row.SpeciesId], row.Name, row.Level, row.CurrentHealth, row.TotalHealth,
            learned[row.Id].Select(entry => entry.MoveId))).ToArray());
    }

    /// <summary>Busca un ejemplar por su identificador y devuelve null si no existe.</summary>
    public async Task<OwnedPokemon?> FindAsync(Guid id, CancellationToken token) => (await Select("WHERE id=@Id", new { Id = id }, token)).SingleOrDefault();

    /// <summary>Devuelve una página de ejemplares ordenada por nombre e identificador.</summary>
    public Task<IReadOnlyList<OwnedPokemon>> ListAsync(CatalogPage page, CancellationToken token) =>
        Select("ORDER BY upper(name),id OFFSET @Offset LIMIT @Limit", page, token);

    /// <summary>Devuelve una página de ejemplares que tienen aprendido el movimiento, ordenada por identificador.</summary>
    public Task<IReadOnlyList<OwnedPokemon>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token) =>
        Select("WHERE id IN(SELECT pokemon_id FROM pokedex_learned_moves WHERE move_id=@MoveId) ORDER BY id OFFSET @Offset LIMIT @Limit", new { MoveId = moveId, page.Offset, page.Limit }, token);

    /// <summary>Recupera todos los ejemplares de la especie para comprobar las reglas que los afectan.</summary>
    public Task<IReadOnlyList<OwnedPokemon>> FindBySpeciesAsync(Guid speciesId, CancellationToken token) =>
        Select("WHERE species_id=@SpeciesId", new { SpeciesId = speciesId }, token);

    /// <summary>Comprueba si existe algún ejemplar de la especie indicada.</summary>
    public Task<bool> ReferencesSpeciesAsync(Guid speciesId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_pokemon WHERE species_id=@SpeciesId)", new { SpeciesId = speciesId }, token);

    /// <summary>Comprueba si algún ejemplar tiene aprendido el movimiento indicado.</summary>
    public Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_learned_moves WHERE move_id=@MoveId)", new { MoveId = moveId }, token);

    /// <summary>Inserta o actualiza el ejemplar y conserva el orden de sus cuatro movimientos aprendidos.</summary>
    public async Task SaveAsync(OwnedPokemon pokemon, CancellationToken token)
    {
        await session.ExecuteAsync("""
            INSERT INTO pokedex_pokemon(id,species_id,name,level,current_health,total_health)
            VALUES(@Id,@SpeciesId,@Name,@Level,@CurrentHealth,@TotalHealth)
            ON CONFLICT(id) DO UPDATE SET species_id=excluded.species_id,name=excluded.name,level=excluded.level,
            current_health=excluded.current_health,total_health=excluded.total_health
            """, new { pokemon.Id, pokemon.SpeciesId, pokemon.Name, pokemon.Level, pokemon.CurrentHealth, pokemon.TotalHealth }, token);
        await session.ExecuteAsync("DELETE FROM pokedex_learned_moves WHERE pokemon_id=@Id", new { pokemon.Id }, token);
        await session.ExecuteAsync("""
            INSERT INTO pokedex_learned_moves(pokemon_id,slot,move_id)
            SELECT @Id,ordinality-1,move_id FROM unnest(@Moves::uuid[]) WITH ORDINALITY AS learned(move_id,ordinality)
            """, new { pokemon.Id, Moves = pokemon.MoveIds.ToArray() }, token);
    }

    /// <summary>Elimina el ejemplar y las referencias a sus movimientos aprendidos dentro de la operación actual.</summary>
    public Task DeleteAsync(Guid id, CancellationToken token) => session.ExecuteAsync("DELETE FROM pokedex_pokemon WHERE id=@Id", new { Id = id }, token);
}
