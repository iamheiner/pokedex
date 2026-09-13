using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain;
using Pokemon.Domain.Battle;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Application.Feature.Battle.Contracts;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle;

internal static class BattleMapping
{
    /// <summary>Captura todos los datos dentro de una misma lectura coherente de la Pokédex.</summary>
    public static async Task<BattlePokemon> SnapshotAsync(IReadRepositoryScope data, Guid id, CancellationToken token)
    {
        var owned = await data.GetReader<IOwnedPokemonReader>().FindAsync(id, token)
            ?? throw new BattleNotFoundException("Referenced Pokemon was not found.");
        var species = await data.GetReader<ISpeciesReader>().FindAsync(owned.SpeciesId, token) ?? throw new InvalidDataException("Pokemon species is missing.");
        var stats = species.Stats;
        var catalogMoves = (await data.GetReader<IMoveReader>().FindManyAsync(owned.MoveIds, token)).ToDictionary(move => move.Id);
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

    /// <summary>Convierte el agregado de partida en una respuesta con participantes, versión, resultado e historial.</summary>
    public static BattleView View(BattleAggregate battle) => new(battle.Id, battle.Version, battle.Phase,
        battle.NextPokemonId, battle.WinnerId, battle.IsDraw, View(battle.First), View(battle.Second), battle.Turns);
    /// <summary>Proyecta el estado de un participante y los usos disponibles de sus movimientos.</summary>
    private static BattlePokemonView View(BattlePokemon participant)
    {
        var p = participant.Snapshot;
        return new(p.Id, p.Name, p.Level, p.Type, p.CurrentHealth, p.TotalHealth, p.Attack, p.Defense,
            p.SpecialAttack, p.SpecialDefense, p.Speed, participant.Moves.All(m => m.RemainingUses == 0),
            participant.Moves.Select(m => new BattleMoveView(m.Id, m.Definition.Name, m.Definition.Type,
                m.Definition.Power, m.RemainingUses)).ToArray());
    }
}
