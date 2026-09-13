using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPokemonSharingMove;

public sealed record GetPokemonSharingMoveQuery(Guid Id, int Offset = 0, int Limit = 100) : IQuery<IReadOnlyList<PokemonView>>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetPokemonSharingMoveQueryHandler(IReadSession reader) : IRequestHandler<GetPokemonSharingMoveQuery, IReadOnlyList<PokemonView>>
{
    /// <summary>Devuelve una página de ejemplares que tienen aprendido el movimiento solicitado.</summary>
    public Task<IReadOnlyList<PokemonView>> Handle(GetPokemonSharingMoveQuery request, CancellationToken token) =>
        reader.ReadAsync<IReadOnlyList<PokemonView>>(async data =>
        {
            await PokedexMapping.MoveAsync(data, request.Id, token);
            return await PokedexMapping.ViewsAsync(data, await data.GetReader<IOwnedPokemonReader>().FindByMoveAsync(request.Id, new CatalogPage(request.Offset, request.Limit), token), token);
        }, token);
}
