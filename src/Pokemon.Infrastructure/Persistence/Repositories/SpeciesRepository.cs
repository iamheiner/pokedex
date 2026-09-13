using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Persistence.Repositories;

internal sealed class SpeciesRepository(CatalogDatabaseSession session) : ISpeciesRepository, ISpeciesReader
{
    private sealed record Row(Guid Id, string Name, int Type, int Health, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed);
    private sealed record LearningRow(Guid SpeciesId, Guid MoveId, int Level);
    private async Task<IReadOnlyList<Species>> Select(string filter, object args, CancellationToken token)
    {
        var rows = (await session.QueryAsync<Row>("""
            SELECT id AS Id,name AS Name,type AS Type,health AS Health,attack AS Attack,defense AS Defense,
            special_attack AS SpecialAttack,special_defense AS SpecialDefense,speed AS Speed FROM pokedex_species
            """ + " " + filter, args, token)).ToArray();
        if (rows.Length == 0) return [];
        var learning = (await session.QueryAsync<LearningRow>("""
            SELECT species_id AS SpeciesId,move_id AS MoveId,level AS Level FROM pokedex_learnset
            WHERE species_id=ANY(@Ids) ORDER BY level,move_id
            """, new { Ids = rows.Select(row => row.Id).ToArray() }, token)).ToLookup(row => row.SpeciesId);
        return DomainMaterializer.Create(() => rows.Select(row => new Species(row.Id, row.Name, (PokemonType)row.Type,
            new BaseStats(row.Health, row.Attack, row.Defense, row.SpecialAttack, row.SpecialDefense, row.Speed),
            learning[row.Id].Select(entry => new LearnableMove(entry.MoveId, entry.Level)))).ToArray());
    }
    public async Task<Species?> FindAsync(Guid id, CancellationToken token) => (await Select("WHERE id=@Id", new { Id = id }, token)).SingleOrDefault();
    public Task<IReadOnlyList<Species>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
        Select("WHERE id=ANY(@Ids)", new { Ids = ids.ToArray() }, token);
    public Task<IReadOnlyList<Species>> ListAsync(CatalogPage page, CancellationToken token) =>
        Select("ORDER BY upper(name),id OFFSET @Offset LIMIT @Limit", page, token);
    public Task<IReadOnlyList<Species>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token) =>
        Select("WHERE id IN(SELECT species_id FROM pokedex_learnset WHERE move_id=@MoveId) ORDER BY id OFFSET @Offset LIMIT @Limit", new { MoveId = moveId, page.Offset, page.Limit }, token);
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_species WHERE upper(name)=upper(@Name) AND id<>@ExceptId)", new { Name = name, ExceptId = exceptId }, token);
    public Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_learnset WHERE move_id=@MoveId)", new { MoveId = moveId }, token);
    public async Task SaveAsync(Species species, CancellationToken token)
    {
        var stats = species.Stats;
        await session.ExecuteAsync("""
            INSERT INTO pokedex_species(id,name,type,health,attack,defense,special_attack,special_defense,speed)
            VALUES(@Id,@Name,@Type,@Health,@Attack,@Defense,@SpecialAttack,@SpecialDefense,@Speed)
            ON CONFLICT(id) DO UPDATE SET name=excluded.name,type=excluded.type,health=excluded.health,attack=excluded.attack,
            defense=excluded.defense,special_attack=excluded.special_attack,special_defense=excluded.special_defense,speed=excluded.speed
            """, new { species.Id, species.Name, Type = (int)species.Type, stats.Health, stats.Attack, stats.Defense, stats.SpecialAttack, stats.SpecialDefense, stats.Speed }, token);
        await session.ExecuteAsync("DELETE FROM pokedex_learnset WHERE species_id=@Id", new { species.Id }, token);
        // Un solo INSERT parametrizado para el plan, independientemente de su tamaño.
        if (species.Learnset.Count > 0)
            await session.ExecuteAsync("""
                INSERT INTO pokedex_learnset(species_id,move_id,level)
                SELECT @Id,move_id,level FROM unnest(@Moves::uuid[],@Levels::integer[]) AS plan(move_id,level)
                """, new { species.Id, Moves = species.Learnset.Select(entry => entry.MoveId).ToArray(), Levels = species.Learnset.Select(entry => entry.Level).ToArray() }, token);
    }
    public Task DeleteAsync(Guid id, CancellationToken token) => session.ExecuteAsync("DELETE FROM pokedex_species WHERE id=@Id", new { Id = id }, token);
}
