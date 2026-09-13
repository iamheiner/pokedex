using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeletePokemon;

public sealed record DeletePokemonCommand(Guid Id) : ICommand<Unit>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class DeletePokemonCommandHandler(IPokedexUnitOfWork transactions) : IRequestHandler<DeletePokemonCommand, Unit>
{
    public Task<Unit> Handle(DeletePokemonCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.PokemonAsync(data, request.Id, token);
            await data.PokemonRepository.DeleteAsync(request.Id, token);
            return Unit.Value;
        }, token);
}
