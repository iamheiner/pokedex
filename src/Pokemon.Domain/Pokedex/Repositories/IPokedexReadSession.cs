namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Puerto CQRS de lectura coherente. No permite abrir una operación de escritura.</summary>
public interface IPokedexReadSession
{
    Task<T> ReadAsync<T>(Func<IPokedexReader, Task<T>> query, CancellationToken token);
}
