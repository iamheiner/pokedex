using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListPokemon;

public sealed record ListPokemonQuery(int Offset = 0, int Limit = 100) : IQuery<IReadOnlyList<PokemonView>>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class ListPokemonQueryHandler(IReadSession reader) : IRequestHandler<ListPokemonQuery, IReadOnlyList<PokemonView>>
{
    /// <summary>Devuelve la página solicitada de ejemplares con sus especies y movimientos.</summary>
    public Task<IReadOnlyList<PokemonView>> Handle(ListPokemonQuery request, CancellationToken token) =>
        reader.ReadAsync<IReadOnlyList<PokemonView>>(async data =>
        {
            return await PokedexMapping.ViewsAsync(data, await data.GetReader<IOwnedPokemonReader>().ListAsync(new CatalogPage(request.Offset, request.Limit), token), token);
        }, token);
}
