using System.Globalization;
using Pokemon.Domain;

namespace Pokemon.Tests;

public sealed class TypeEffectivenessTests
{
    /// <summary>Carga los casos independientes de efectividad entre tipos de movimiento y defensor.</summary>
    public static IEnumerable<object[]> Matchups()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Data", "type-chart.csv"));
        var defenders = lines[0].Split(',').Skip(1).Select(Enum.Parse<PokemonType>).ToArray();
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(',');
            for (var i = 0; i < defenders.Length; i++)
                yield return [Enum.Parse<PokemonType>(cells[0]), defenders[i], decimal.Parse(cells[i + 1], CultureInfo.InvariantCulture)];
        }
    }

    /// <summary>Comprueba que la efectividad calculada coincide con la tabla esperada del enunciado.</summary>
    [Theory]
    [MemberData(nameof(Matchups))]
    public void MatchesEnunciado(PokemonType attack, PokemonType defense, decimal expected) =>
        Assert.Equal(expected, TypeEffectiveness.Against(attack, defense));

    /// <summary>Comprueba que los datos de prueba contienen exactamente una vez cada combinación de tipos.</summary>
    [Fact]
    public void FixtureCoversEveryPairExactlyOnce()
    {
        var rows = Matchups().ToArray();
        var pairs = rows.Select(row => ((PokemonType)row[0], (PokemonType)row[1])).ToHashSet();
        Assert.Equal(324, rows.Length);
        Assert.Equal(rows.Length, pairs.Count);
        foreach (var attack in Enum.GetValues<PokemonType>())
            foreach (var defense in Enum.GetValues<PokemonType>())
                Assert.Contains((attack, defense), pairs);
    }

    /// <summary>Comprueba que la tabla de efectividad rechaza tipos desconocidos.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 999)]
    public void RejectsUnknownTypes(int attack, int defense) =>
        Assert.Throws<ArgumentException>(() => TypeEffectiveness.Against((PokemonType)attack, (PokemonType)defense));
}
