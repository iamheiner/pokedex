namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Repositorio de CatalogMove dentro de una unidad de trabajo; no confirma cambios por sí solo.</summary>
public interface IMoveRepository
{
    IReadOnlyCollection<CatalogMove> List();
    CatalogMove? Find(Guid id);
    void Save(CatalogMove aggregate);
    void Delete(Guid id);
}
