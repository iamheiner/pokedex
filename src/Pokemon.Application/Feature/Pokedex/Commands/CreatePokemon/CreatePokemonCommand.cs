using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreatePokemon;

/// <summary>Creación de Pokemon: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record CreatePokemonCommand(PokemonInput Data) : IRequest<PokemonView>;

public sealed class CreatePokemonCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<CreatePokemonCommand, PokemonView>
{
    public Task<PokemonView> Handle(CreatePokemonCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            var entity = PokedexChanges.Pokemon(data, Guid.NewGuid(), request.Data);
            data.PokemonRepository.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
