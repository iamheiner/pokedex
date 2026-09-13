using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Una fila inválida es corrupción de almacenamiento, no un error en la petición HTTP.</summary>
internal static class DomainMaterializer
{
    public static T Create<T>(Func<T> factory)
    {
        try { return factory(); }
        catch (PokedexRuleException error) { throw new InvalidDataException("Stored Pokedex data violates domain invariants.", error); }
    }
}
