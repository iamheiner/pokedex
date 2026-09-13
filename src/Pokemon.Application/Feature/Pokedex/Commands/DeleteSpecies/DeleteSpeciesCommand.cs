using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeleteSpecies;

/// <summary>Eliminación de Species: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record DeleteSpeciesCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteSpeciesCommandHandler(IPokedexStore store) : IRequestHandler<DeleteSpeciesCommand, Unit>
{
    public Task<Unit> Handle(DeleteSpeciesCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Species(data, request.Id);
            if (data.Pokemon.Any(p => p.SpeciesId == request.Id))
                throw new PokedexConflictException("Species is referenced by an owned Pokemon.");
            data.DeleteSpecies(request.Id);
            return Unit.Value;
        }, token);
}
