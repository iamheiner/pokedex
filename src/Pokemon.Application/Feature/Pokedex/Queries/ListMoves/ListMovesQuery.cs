using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListMoves;

public sealed record ListMovesQuery(int Offset = 0, int Limit = 100) : IQuery<IReadOnlyList<MoveView>>;

/// <summary>
/// Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.
/// </summary>
public sealed class ListMovesQueryHandler(IReadSession reader) : IRequestHandler<ListMovesQuery, IReadOnlyList<MoveView>>
{
    /// <summary>
    /// Devuelve la página solicitada de movimientos del catálogo.
    /// </summary>
    public Task<IReadOnlyList<MoveView>> Handle(ListMovesQuery request, CancellationToken token) =>
        reader.ReadAsync<IReadOnlyList<MoveView>>(async data =>
        {
            return (await data.GetReader<IMoveReader>().ListAsync(new CatalogPage(request.Offset, request.Limit), token)).Select(PokedexMapping.View).ToArray();
        }, token);
}
