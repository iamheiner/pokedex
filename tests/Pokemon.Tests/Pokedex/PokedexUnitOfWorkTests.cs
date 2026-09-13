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
        using var unitOfWork = new InMemoryPokedexUnitOfWork(false);
        var moves = Enumerable.Range(1, 4).Select(index =>
            new CatalogMove(Guid.NewGuid(), $"Move {index}", 40, PokemonType.Normal)).ToArray();
        var species = new Species(Guid.NewGuid(), "Species", PokemonType.Normal,
            new BaseStats(40, 40, 40, 40, 40, 40), moves.Select(move => new LearnableMove(move.Id, 1)));
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Pokemon", 20, 40, 40, moves.Select(move => move.Id));
        async Task Execute() => await unitOfWork.Write(session =>
        {
            foreach (var move in moves) session.MoveRepository.Save(move);
            session.SpeciesRepository.Save(species);
            session.PokemonRepository.Save(pokemon);
            Assert.Same(species, session.SpeciesRepository.Find(species.Id));
            Assert.Same(pokemon, session.PokemonRepository.Find(pokemon.Id));
            Assert.Same(moves[0], session.MoveRepository.Find(moves[0].Id));
            if (fail) throw new InvalidOperationException("Abort all repositories");
            return true;
        }, default);
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(Execute);
        else await Execute();
        var counts = await unitOfWork.Read(reader => (reader.Moves.Count, reader.Species.Count, reader.Pokemon.Count), default);
        Assert.Equal(fail ? (0, 0, 0) : (4, 1, 1), counts);
    }
}
