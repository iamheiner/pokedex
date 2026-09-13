using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreatePokemon;

public sealed record CreatePokemonCommand(PokemonInput Data) : ICommand<PokemonView>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class CreatePokemonCommandHandler(IUnitOfWork transactions) : IRequestHandler<CreatePokemonCommand, PokemonView>
{
    public Task<PokemonView> Handle(CreatePokemonCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            var entity = await PokedexChanges.PokemonAsync(data, Guid.NewGuid(), request.Data, token);
            await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(entity, token);
            return await PokedexMapping.ViewAsync(data, entity, token);
        }, token);
}
