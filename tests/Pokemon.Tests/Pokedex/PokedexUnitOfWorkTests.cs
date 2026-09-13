using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Tests.Persistence;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Infrastructure.Pokedex;

namespace Pokemon.Tests.Pokedex;

public sealed class PokedexUnitOfWorkTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Three_repositories_publish_together_or_roll_back_together(bool fail)
    {
        using var unitOfWork = new InMemoryUnitOfWork(false);
        var moves = Enumerable.Range(1, 4).Select(index =>
            new CatalogMove(Guid.NewGuid(), $"Move {index}", 40, PokemonType.Normal)).ToArray();
        var species = new Species(Guid.NewGuid(), "Species", PokemonType.Normal,
            new BaseStats(40, 40, 40, 40, 40, 40), moves.Select(move => new LearnableMove(move.Id, 1)));
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Pokemon", 20, 40, 40, moves.Select(move => move.Id));
        async Task Execute() => await unitOfWork.WriteAsync(async session =>
        {
            foreach (var move in moves) await session.GetRepository<IMoveRepository>().SaveAsync(move, default);
            await session.GetRepository<ISpeciesRepository>().SaveAsync(species, default);
            await session.GetRepository<IOwnedPokemonRepository>().SaveAsync(pokemon, default);
            Assert.Same(species, (await session.GetReader<ISpeciesReader>().FindAsync(species.Id, default)));
            Assert.Same(pokemon, (await session.GetReader<IOwnedPokemonReader>().FindAsync(pokemon.Id, default)));
            Assert.Same(moves[0], (await session.GetReader<IMoveReader>().FindAsync(moves[0].Id, default)));
            if (fail) throw new InvalidOperationException("Abort all repositories");
            return true;
        }, default);
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(Execute);
        else await Execute();
        var counts = await unitOfWork.ReadAsync(async reader => ((await reader.GetReader<IMoveReader>().ListAsync(new(), default)).Count, (await reader.GetReader<ISpeciesReader>().ListAsync(new(), default)).Count, (await reader.GetReader<IOwnedPokemonReader>().ListAsync(new(), default)).Count), default);
        Assert.Equal(fail ? (0, 0, 0) : (4, 1, 1), counts);
    }
}
