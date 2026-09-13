using Pokemon.Domain;
using Pokemon.Domain.Battle;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Application.Feature.Battle.Contracts;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle;

internal static class BattleMapping
{
    /// <summary>Captura todos los datos dentro de una misma lectura coherente de la Pokédex.</summary>
    public static async Task<BattlePokemon> SnapshotAsync(IPokedexReader data, Guid id, CancellationToken token)
    {
        var owned = await data.Pokemon.FindAsync(id, token)
            ?? throw new BattleNotFoundException("Referenced Pokemon was not found.");
        var species = await data.Species.FindAsync(owned.SpeciesId, token) ?? throw new InvalidDataException("Pokemon species is missing.");
        var stats = species.Stats;
        var catalogMoves = (await data.Moves.FindManyAsync(owned.MoveIds, token)).ToDictionary(move => move.Id);
        var moves = owned.MoveIds.Select(moveId =>
        {
            var catalog = catalogMoves[moveId];
            return new BattleMove(catalog.Id, catalog.ToDamageMove());
        }).ToArray();
        var pokemon = new Combatant(id: owned.Id, name: owned.Name, level: owned.Level, type: species.Type,
            currentHealth: owned.CurrentHealth, totalHealth: owned.TotalHealth, attack: stats.Attack,
            defense: stats.Defense, specialAttack: stats.SpecialAttack, specialDefense: stats.SpecialDefense,
            speed: stats.Speed, moves: moves.Select(m => m.Definition));
        return new(pokemon, moves);
    }

    public static BattleView View(BattleAggregate battle) => new(battle.Id, battle.Version, battle.Phase,
        battle.NextPokemonId, battle.WinnerId, battle.IsDraw, View(battle.First), View(battle.Second), battle.Turns);
    private static BattlePokemonView View(BattlePokemon participant)
    {
        var p = participant.Snapshot;
        return new(p.Id, p.Name, p.Level, p.Type, p.CurrentHealth, p.TotalHealth, p.Attack, p.Defense,
            p.SpecialAttack, p.SpecialDefense, p.Speed, participant.Moves.All(m => m.RemainingUses == 0),
            participant.Moves.Select(m => new BattleMoveView(m.Id, m.Definition.Name, m.Definition.Type,
                m.Definition.Power, m.RemainingUses)).ToArray());
    }
}
