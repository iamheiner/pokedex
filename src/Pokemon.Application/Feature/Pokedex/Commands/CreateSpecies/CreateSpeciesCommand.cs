using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateSpecies;

/// <summary>Creación de Species: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record CreateSpeciesCommand(SpeciesInput Data) : IRequest<SpeciesView>;

public sealed class CreateSpeciesCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<CreateSpeciesCommand, SpeciesView>
{
    public Task<SpeciesView> Handle(CreateSpeciesCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            var entity = PokedexChanges.Species(data, Guid.NewGuid(), request.Data);
            data.SpeciesRepository.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
