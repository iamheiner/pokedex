using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdatePokemon;

public sealed record UpdatePokemonCommand(Guid Id, PokemonInput Data) : ICommand<PokemonView>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class UpdatePokemonCommandHandler(IPokedexUnitOfWork transactions) : IRequestHandler<UpdatePokemonCommand, PokemonView>
{
    public Task<PokemonView> Handle(UpdatePokemonCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.PokemonAsync(data, request.Id, token);
            var entity = await PokedexChanges.PokemonAsync(data, request.Id, request.Data, token);
            await data.PokemonRepository.SaveAsync(entity, token);
            return await PokedexMapping.ViewAsync(data, entity, token);
        }, token);
}
