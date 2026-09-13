using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdatePokemon;

/// <summary>Actualización de Pokemon: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record UpdatePokemonCommand(Guid Id, PokemonInput Data) : IRequest<PokemonView>;

public sealed class UpdatePokemonCommandHandler(IPokedexStore store) : IRequestHandler<UpdatePokemonCommand, PokemonView>
{
    public Task<PokemonView> Handle(UpdatePokemonCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Pokemon(data, request.Id);
            var entity = PokedexChanges.Pokemon(data, request.Id, request.Data);
            data.Save(entity);
            return PokedexMapping.View(data, entity);
        }, token);
}
