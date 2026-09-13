using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdateSpecies;

public sealed record UpdateSpeciesCommand(Guid Id, SpeciesInput Data) : ICommand<SpeciesView>;

/// <summary>
/// Coordina reglas y persistencia del agregado en una única transacción.
/// </summary>
public sealed class UpdateSpeciesCommandHandler(IUnitOfWork transactions) : IRequestHandler<UpdateSpeciesCommand, SpeciesView>
{
    /// <summary>
    /// Valida y reemplaza una especie sin invalidar el aprendizaje de sus ejemplares.
    /// </summary>
    public Task<SpeciesView> Handle(UpdateSpeciesCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.SpeciesAsync(data, request.Id, token);
            var entity = await PokedexChanges.SpeciesAsync(data, request.Id, request.Data, token);
            await data.GetRepository<ISpeciesRepository>().SaveAsync(entity, token);
            return await PokedexMapping.ViewAsync(data, entity, token);
        }, token);
}
