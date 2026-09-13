using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateSpecies;

public sealed record CreateSpeciesCommand(SpeciesInput Data) : ICommand<SpeciesView>;

/// <summary>
/// Coordina reglas y persistencia del agregado en una única transacción.
/// </summary>
public sealed class CreateSpeciesCommandHandler(IUnitOfWork transactions) : IRequestHandler<CreateSpeciesCommand, SpeciesView>
{
    /// <summary>
    /// Valida, guarda y devuelve una nueva especie dentro de una transacción.
    /// </summary>
    public Task<SpeciesView> Handle(CreateSpeciesCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            var entity = await PokedexChanges.SpeciesAsync(data, Guid.NewGuid(), request.Data, token);
            await data.GetRepository<ISpeciesRepository>().SaveAsync(entity, token);
            return await PokedexMapping.ViewAsync(data, entity, token);
        }, token);
}
