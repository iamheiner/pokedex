namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Persistencia del agregado Species dentro de una unidad de trabajo.</summary>
public interface ISpeciesRepository : ISpeciesReader
{
    Task SaveAsync(Species aggregate, CancellationToken token);
    Task DeleteAsync(Guid id, CancellationToken token);
}
