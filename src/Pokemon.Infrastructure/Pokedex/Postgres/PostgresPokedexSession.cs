using Npgsql;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

/// <summary>Una conexión y una transacción para los tres repositorios.</summary>
internal sealed class PostgresPokedexSession : IPokedexSession
{
    private readonly PostgresMoveRepository moves = new();
    private readonly PostgresSpeciesRepository species = new();
    private readonly PostgresOwnedPokemonRepository pokemon = new();
    public ISpeciesRepository SpeciesRepository => species;
    public IMoveRepository MoveRepository => moves;
    public IOwnedPokemonRepository PokemonRepository => pokemon;
    public IReadOnlyCollection<Species> Species => species.List();
    public IReadOnlyCollection<CatalogMove> Moves => moves.List();
    public IReadOnlyCollection<OwnedPokemon> Pokemon => pokemon.List();
    public async Task Load(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        try
        {
            await moves.Load(connection,transaction,token);
            await species.Load(connection,transaction,token);
            await pokemon.Load(species,connection,transaction,token);
        }
        catch (PokedexRuleException error)
        {
            throw new InvalidDataException("Stored Pokedex data violates domain invariants.", error);
        }
    }
    public async Task Flush(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await moves.Flush(connection,transaction,token);
        await species.Flush(connection,transaction,token);
        await pokemon.Flush(connection,transaction,token);
    }
}
