using Pokemon.Application.Feature.Damage;

namespace Pokemon.Api.Feature.Damage;

// Adaptador del puerto de aplicación: el límite superior de Random.Next es exclusivo.
internal sealed class DamageRandom : IDamageRandom
{
    /// <summary>Devuelve un porcentaje aleatorio entero entre 85 y 100, ambos incluidos.</summary>
    public int Next() => Random.Shared.Next(85, 101);
}
