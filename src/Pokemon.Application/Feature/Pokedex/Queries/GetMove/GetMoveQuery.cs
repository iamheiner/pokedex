using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetMove;

public sealed record GetMoveQuery(Guid Id) : IQuery<MoveView>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetMoveQueryHandler(IReadSession reader) : IRequestHandler<GetMoveQuery, MoveView>
{
    public Task<MoveView> Handle(GetMoveQuery request, CancellationToken token) =>
        reader.ReadAsync<MoveView>(async data =>
        {
            return PokedexMapping.View(await PokedexMapping.MoveAsync(data, request.Id, token));
        }, token);
}
