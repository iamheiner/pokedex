using System.Text.Json;
using System.Text.Json.Nodes;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Infrastructure.Persistence.Repositories;
using Pokemon.Infrastructure.Persistence.Migrations;
using Pokemon.Infrastructure.Persistence.Serialization;
namespace Pokemon.Tests.Battle;

public sealed class BattleDocumentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(59)]
    public void RoundTripPreservesEveryStateIncludingStruggleAndDraw(int turns)
    {
        var battle = BattleDomainTests.Duel(19, 19);
        for (var index = 0; index < turns; index++) battle = BattleDomainTests.Next(battle);
        var restored = BattleDocumentCodec.Deserialize(BattleDocumentCodec.Serialize(battle));
        Assert.Equal(JsonSerializer.Serialize(battle), JsonSerializer.Serialize(restored));
    }
    [Theory]
    [InlineData("format")]
    [InlineData("version")]
    [InlineData("damage")]
    [InlineData("actor")]
    [InlineData("move")]
    [InlineData("null-move")]
    public void InconsistentDocumentsFailAsStorageErrors(string fault)
    {
        var battle = BattleDomainTests.Next(BattleDomainTests.Duel());
        var json = JsonNode.Parse(BattleDocumentCodec.Serialize(battle))!;
        switch (fault)
        {
            case "format": json["formatVersion"] = 2; break;
            case "version": json["version"] = 5; break;
            case "damage": json["turns"]![0]!["appliedDamage"] = 10; break;
            case "actor": json["turns"]![0]!["attackerId"] = Guid.NewGuid(); break;
            case "move": json["first"]!["moves"]![0]!["power"] = 0; break;
            case "null-move": json["first"]!["moves"]![0] = null; break;
        }
        Assert.Throws<InvalidDataException>(() => BattleDocumentCodec.Deserialize(json.ToJsonString()));
    }
    [Fact]
    public void NonImmuneDamageAndRecoilPreserveInitialHealth()
    {
        var battle = Pokemon.Domain.Battle.Battle.Start(Guid.NewGuid(), BattleDomainTests.Fighter(), BattleDomainTests.Fighter());
        battle = BattleDomainTests.Next(battle);
        Assert.Equal(JsonSerializer.Serialize(battle), JsonSerializer.Serialize(BattleDocumentCodec.Deserialize(BattleDocumentCodec.Serialize(battle))));
    }
}
