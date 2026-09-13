namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Límite explícito de una consulta de catálogo; nunca representa una carga completa implícita.</summary>
public sealed record CatalogPage
{
    public int Offset { get; }
    public int Limit { get; }
    public CatalogPage(int offset = 0, int limit = 100)
    {
        if (offset < 0 || limit is < 1 or > 100)
            throw new PokedexRuleException("Offset must be non-negative and limit must be between 1 and 100.");
        Offset = offset; Limit = limit;
    }
}
