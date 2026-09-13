using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeletePokemon;

/// <summary>Eliminación de Pokemon: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record DeletePokemonCommand(Guid Id) : IRequest<Unit>;

public sealed class DeletePokemonCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<DeletePokemonCommand, Unit>
{
    public Task<Unit> Handle(DeletePokemonCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Pokemon(data, request.Id);
            data.PokemonRepository.Delete(request.Id);
            return Unit.Value;
        }, token);
}
