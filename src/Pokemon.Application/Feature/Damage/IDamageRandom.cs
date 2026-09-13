namespace Pokemon.Application.Feature.Damage;

/// <summary>Proporciona el porcentaje aleatorio del cálculo de daño, entre 85 y 100.</summary>
public interface IDamageRandom
{
    int Next();
}
