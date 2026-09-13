namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Repositorio de Species dentro de una unidad de trabajo; no confirma cambios por sí solo.</summary>
public interface ISpeciesRepository
{
    IReadOnlyCollection<Species> List();
    Species? Find(Guid id);
    void Save(Species aggregate);
    void Delete(Guid id);
}
