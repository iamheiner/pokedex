using MediatR;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Domain.Battle.Repositories;
namespace Pokemon.Application.Feature.Battle.Queries.GetBattle;

public sealed record GetBattleQuery(Guid Id) : IRequest<BattleView>;
/// <summary>Devuelve estado, siguiente actor, versión e historial sin ejecutar ninguna acción.</summary>
public sealed class GetBattleQueryHandler(IBattleRepository battles) : IRequestHandler<GetBattleQuery, BattleView>
{
    public async Task<BattleView> Handle(GetBattleQuery request, CancellationToken token) =>
        BattleMapping.View(await battles.Get(request.Id, token));
}
