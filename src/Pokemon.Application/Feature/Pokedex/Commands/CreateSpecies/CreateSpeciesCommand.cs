using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateSpecies;

/// <summary>Creación de Species: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record CreateSpeciesCommand(SpeciesInput Data) : IRequest<SpeciesView>;

public sealed class CreateSpeciesCommandHandler(IPokedexStore store) : IRequestHandler<CreateSpeciesCommand, SpeciesView>
{
    public Task<SpeciesView> Handle(CreateSpeciesCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            var entity = PokedexChanges.Species(data, Guid.NewGuid(), request.Data);
            data.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
