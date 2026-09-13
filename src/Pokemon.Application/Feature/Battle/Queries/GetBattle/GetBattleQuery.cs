using Pokemon.Domain.Common.Persistence;
using Pokemon.Application.Common.Messaging;
using MediatR;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Domain.Battle.Repositories;
namespace Pokemon.Application.Feature.Battle.Queries.GetBattle;

public sealed record GetBattleQuery(Guid Id) : IQuery<BattleView>;
/// <summary>Devuelve estado, siguiente actor, versión e historial sin ejecutar ninguna acción.</summary>
public sealed class GetBattleQueryHandler(IReadSession reader) : IRequestHandler<GetBattleQuery, BattleView>
{
    public async Task<BattleView> Handle(GetBattleQuery request, CancellationToken token) =>
        await reader.ReadAsync(async data => BattleMapping.View(await data.GetReader<IBattleReader>().Get(request.Id, token)), token);
}
