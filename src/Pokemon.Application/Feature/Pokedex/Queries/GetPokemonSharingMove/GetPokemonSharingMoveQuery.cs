using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPokemonSharingMove;

/// <summary>Consulta GetPokemonSharingMove: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetPokemonSharingMoveQuery(Guid Id) : IRequest<IReadOnlyList<PokemonView>>;

public sealed class GetPokemonSharingMoveQueryHandler(IPokedexUnitOfWork store) : IRequestHandler<GetPokemonSharingMoveQuery, IReadOnlyList<PokemonView>>
{
    public Task<IReadOnlyList<PokemonView>> Handle(GetPokemonSharingMoveQuery request, CancellationToken token) =>
        store.Read<IReadOnlyList<PokemonView>>(data => SharingPokemon(data, request.Id), token);
    private static IReadOnlyList<PokemonView> SharingPokemon(IPokedexReader data, Guid id)
    {
        PokedexMapping.Move(data, id);
        return data.Pokemon.Where(p => p.MoveIds.Contains(id)).OrderBy(p => p.Id).Select(p => PokedexMapping.View(data, p)).ToArray();
    }
}
