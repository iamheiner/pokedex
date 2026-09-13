using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Infrastructure.Pokedex;

/// <summary>Datos de demostración; fuentes y simplificaciones en docs/pokedex.md.</summary>
internal static class PokedexSeed
{
    private static Guid Id(int number) => Guid.Parse($"00000000-0000-0000-0000-{number:000000000000}");
    public static async Task PopulateAsync(IRepositoryScope data, CancellationToken token)
    {
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(1), "Scratch", 40, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(2), "Ember", 40, PokemonType.Fire), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(3), "Dragon Breath", 60, PokemonType.Dragon), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(4), "Fire Fang", 65, PokemonType.Fire), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(5), "Flamethrower", 90, PokemonType.Fire), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(6), "Tackle", 40, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(7), "Water Gun", 40, PokemonType.Water), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(8), "Rapid Spin", 50, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(9), "Bite", 60, PokemonType.Dark), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(10), "Water Pulse", 60, PokemonType.Water), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(11), "Nuzzle", 20, PokemonType.Electric), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(12), "Quick Attack", 40, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(13), "Thunder Shock", 40, PokemonType.Electric), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(14), "Feint", 30, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(15), "Spark", 65, PokemonType.Electric), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(16), "Poison Sting", 15, PokemonType.Poison), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(17), "Rollout", 30, PokemonType.Rock), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(18), "Fury Cutter", 40, PokemonType.Bug), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(19), "Bulldoze", 60, PokemonType.Ground), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(20), "Covet", 60, PokemonType.Normal), token);
        await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Id(21), "Swift", 60, PokemonType.Normal), token);
        var species1 = new Species(Id(101), "Charmander", PokemonType.Fire,
            new BaseStats(39, 52, 43, 60, 50, 65),
            [new LearnableMove(Id(1), 1), new LearnableMove(Id(2), 4), new LearnableMove(Id(3), 12), new LearnableMove(Id(4), 17), new LearnableMove(Id(5), 24)]);
        await data.GetRepository<ISpeciesRepository>().SaveAsync(species1, token);
        await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(Id(201), species1, "Charmander", 20, 39, 39,
            [Id(1), Id(2), Id(3), Id(4)]), token);
        var species2 = new Species(Id(102), "Squirtle", PokemonType.Water,
            new BaseStats(44, 48, 65, 50, 64, 43),
            [new LearnableMove(Id(6), 1), new LearnableMove(Id(7), 3), new LearnableMove(Id(8), 9), new LearnableMove(Id(9), 12), new LearnableMove(Id(10), 15)]);
        await data.GetRepository<ISpeciesRepository>().SaveAsync(species2, token);
        await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(Id(202), species2, "Squirtle", 20, 44, 44,
            [Id(6), Id(7), Id(8), Id(9)]), token);
        var species3 = new Species(Id(103), "Pikachu", PokemonType.Electric,
            new BaseStats(35, 55, 40, 50, 50, 90),
            [new LearnableMove(Id(11), 1), new LearnableMove(Id(12), 1), new LearnableMove(Id(13), 1), new LearnableMove(Id(14), 16), new LearnableMove(Id(15), 20)]);
        await data.GetRepository<ISpeciesRepository>().SaveAsync(species3, token);
        await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(Id(203), species3, "Pikachu", 20, 35, 35,
            [Id(11), Id(12), Id(13), Id(14)]), token);
        var species4 = new Species(Id(104), "Sandshrew", PokemonType.Ground,
            new BaseStats(50, 75, 85, 20, 30, 40),
            [new LearnableMove(Id(1), 1), new LearnableMove(Id(16), 3), new LearnableMove(Id(17), 9), new LearnableMove(Id(18), 12), new LearnableMove(Id(8), 15), new LearnableMove(Id(19), 18)]);
        await data.GetRepository<ISpeciesRepository>().SaveAsync(species4, token);
        await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(Id(204), species4, "Sandshrew", 20, 50, 50,
            [Id(1), Id(16), Id(17), Id(18)]), token);
        var species5 = new Species(Id(105), "Eevee", PokemonType.Normal,
            new BaseStats(55, 55, 50, 45, 65, 55),
            [new LearnableMove(Id(20), 1), new LearnableMove(Id(6), 1), new LearnableMove(Id(12), 10), new LearnableMove(Id(21), 20), new LearnableMove(Id(9), 25)]);
        await data.GetRepository<ISpeciesRepository>().SaveAsync(species5, token);
        await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(Id(205), species5, "Eevee", 20, 55, 55,
            [Id(20), Id(6), Id(12), Id(21)]), token);
    }
}
