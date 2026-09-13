using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListPokemon;

/// <summary>Consulta ListPokemon: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record ListPokemonQuery() : IRequest<IReadOnlyList<PokemonView>>;

public sealed class ListPokemonQueryHandler(IPokedexUnitOfWork store) : IRequestHandler<ListPokemonQuery, IReadOnlyList<PokemonView>>
{
    public Task<IReadOnlyList<PokemonView>> Handle(ListPokemonQuery request, CancellationToken token) =>
        store.Read<IReadOnlyList<PokemonView>>(data => data.Pokemon.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).Select(e => PokedexMapping.View(data, e)).ToArray(), token);

}
