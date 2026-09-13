using Pokemon.Domain.Battle.Exceptions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pokemon.Domain;
using Pokemon.Domain.Battle;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Infrastructure.Persistence.Serialization;

/// <summary>
/// Formato durable v1: instantáneas iniciales y acciones resueltas con su azar ya fijado.
/// Reconstruye mediante las reglas del dominio, sin setters de persistencia ni nuevo azar.
/// El historial está acotado a menos de 60 acciones. Cambiar reglas exige versionar este formato.
/// </summary>
public static class BattleDocumentCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter<PokemonType>(allowIntegerValues: false) }
    };

    public static string Serialize(BattleAggregate battle) => JsonSerializer.Serialize(new Document(
        1, battle.Id, battle.Version, Initial(battle.First, battle.Turns), Initial(battle.Second, battle.Turns), battle.Turns.ToArray()), Json);

    public static BattleAggregate Deserialize(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<Document>(json, Json) ?? throw new InvalidDataException("Missing battle document.");
            if (document.FormatVersion != 1 || document.Turns.Length >= 60 || document.Version != document.Turns.Length + 1)
                throw new InvalidDataException("Unsupported or inconsistent battle document.");
            var battle = BattleAggregate.Start(document.Id, Restore(document.First), Restore(document.Second));
            foreach (var turn in document.Turns)
            {
                if (turn is null) throw new InvalidDataException("Missing stored turn.");
                battle = battle.PlayTurn(turn.AttackerId, turn.MoveId, battle.Version, turn.RandomFactor ?? 100);
                if (battle.Turns[^1] != turn) throw new InvalidDataException("Stored turn does not match the battle rules.");
            }
            return battle;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or BattleRuleException or BattleConflictException)
        {
            // Datos dañados o de otro formato son un fallo de almacenamiento, nunca HTTP 400 del cliente.
            throw new InvalidDataException("The stored battle could not be reconstructed.", error);
        }
    }

    private static PokemonDocument Initial(BattlePokemon participant, IReadOnlyList<BattleTurn> turns)
    {
        var p = participant.Snapshot;
        // La única variación de salud es daño/retroceso. Recuperar la salud inicial conserva
        // también partidas que se añadan al almacén después de haber resuelto alguna acción.
        var initialHealth = p.CurrentHealth + turns.Sum(t => t.AttackerId == p.Id ? t.RecoilDamage : t.AppliedDamage);
        return new(p.Id, p.Name, p.Level, p.Type, initialHealth, p.TotalHealth, p.Attack, p.Defense,
            p.SpecialAttack, p.SpecialDefense, p.Speed,
            participant.Moves.Select(m => new MoveDocument(m.Id, m.Definition.Name, m.Definition.Power, m.Definition.Type)).ToArray());
    }

    private static BattlePokemon Restore(PokemonDocument p)
    {
        if (p.Moves.Any(m => m is null)) throw new InvalidDataException("Missing stored move.");
        var moves = p.Moves.Select(m => new BattleMove(m.Id, new Move(m.Name, m.Power, m.Type))).ToArray();
        var snapshot = new Combatant(id: p.Id, name: p.Name, level: p.Level, type: p.Type,
            currentHealth: p.CurrentHealth, totalHealth: p.TotalHealth, attack: p.Attack, defense: p.Defense,
            specialAttack: p.SpecialAttack, specialDefense: p.SpecialDefense, speed: p.Speed,
            moves: moves.Select(m => m.Definition));
        return new(snapshot, moves);
    }

    private sealed record Document(int FormatVersion, Guid Id, int Version, PokemonDocument First, PokemonDocument Second, BattleTurn[] Turns);
    private sealed record PokemonDocument(Guid Id, string Name, int Level, PokemonType Type, int CurrentHealth,
        int TotalHealth, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed, MoveDocument[] Moves);
    private sealed record MoveDocument(Guid Id, string Name, int Power, PokemonType Type);
}
