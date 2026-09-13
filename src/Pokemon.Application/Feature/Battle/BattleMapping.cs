using Pokemon.Domain;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Pokedex.Persistence;
using Pokemon.Application.Feature.Battle.Contracts;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle;

internal static class BattleMapping
{
    /// <summary>Captura todos los datos dentro de una misma lectura coherente de la Pokédex.</summary>
    public static BattlePokemon Snapshot(IPokedexReader data, Guid id)
    {
        var owned = data.Pokemon.SingleOrDefault(p => p.Id == id)
            ?? throw new BattleNotFoundException("Referenced Pokemon was not found.");
        var species = data.Species.Single(s => s.Id == owned.SpeciesId);
        var stats = species.Stats;
        var moves = owned.MoveIds.Select(moveId =>
        {
            var catalog = data.Moves.Single(m => m.Id == moveId);
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
