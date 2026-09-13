using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeletePokemon;

public sealed record DeletePokemonCommand(Guid Id) : ICommand<Unit>;

/// <summary>
/// Coordina reglas y persistencia del agregado en una única transacción.
/// </summary>
public sealed class DeletePokemonCommandHandler(IUnitOfWork transactions) : IRequestHandler<DeletePokemonCommand, Unit>
{
    /// <summary>
    /// Elimina el ejemplar indicado después de comprobar que existe.
    /// </summary>
    public Task<Unit> Handle(DeletePokemonCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.PokemonAsync(data, request.Id, token);
            await data.GetRepository<IOwnedPokemonRepository>().DeleteAsync(request.Id, token);
            return Unit.Value;
        }, token);
}
