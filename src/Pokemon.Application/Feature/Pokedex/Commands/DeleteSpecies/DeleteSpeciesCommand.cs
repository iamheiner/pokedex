using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeleteSpecies;

public sealed record DeleteSpeciesCommand(Guid Id) : ICommand<Unit>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class DeleteSpeciesCommandHandler(IUnitOfWork transactions) : IRequestHandler<DeleteSpeciesCommand, Unit>
{
    public Task<Unit> Handle(DeleteSpeciesCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.SpeciesAsync(data, request.Id, token);
            if (await data.GetReader<IOwnedPokemonReader>().ReferencesSpeciesAsync(request.Id, token))
                throw new PokedexConflictException("Species is referenced by an owned Pokemon.");
            await data.GetRepository<ISpeciesRepository>().DeleteAsync(request.Id, token);
            return Unit.Value;
        }, token);
}
