using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdateSpecies;

/// <summary>Actualización de Species: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record UpdateSpeciesCommand(Guid Id, SpeciesInput Data) : IRequest<SpeciesView>;

public sealed class UpdateSpeciesCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<UpdateSpeciesCommand, SpeciesView>
{
    public Task<SpeciesView> Handle(UpdateSpeciesCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Species(data, request.Id);
            var entity = PokedexChanges.Species(data, request.Id, request.Data);
            data.SpeciesRepository.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
