namespace Pokemon.Application.Feature.Damage;

/// <summary>Proporciona el porcentaje aleatorio del cálculo de daño, entre 85 y 100.</summary>
public interface IDamageRandom
{
    /// <summary>Obtiene el porcentaje aleatorio entre 85 y 100 que utilizará el cálculo de daño.</summary>
    int Next();
}
