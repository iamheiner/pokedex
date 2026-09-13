using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdatePokemon;

/// <summary>Actualización de Pokemon: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record UpdatePokemonCommand(Guid Id, PokemonInput Data) : IRequest<PokemonView>;

public sealed class UpdatePokemonCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<UpdatePokemonCommand, PokemonView>
{
    public Task<PokemonView> Handle(UpdatePokemonCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Pokemon(data, request.Id);
            var entity = PokedexChanges.Pokemon(data, request.Id, request.Data);
            data.PokemonRepository.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
