namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Generic repository interface defining standard CRUD operations for any entity.
/// Follows the Repository pattern - abstracts data access so the Application layer
/// never depends directly on EF Core or any other persistence technology.
/// </summary>
/// <typeparam name="T">The entity type this repository manages.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>Returns the entity with the given id, or null if not found.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns a no-tracking queryable so callers can compose WHERE/ORDER/SELECT before materializing.</summary>
    IQueryable<T> AsQueryable();

    /// <summary>Persists a new entity and returns it after saving.</summary>
    Task<T> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Saves changes to an existing tracked or detached entity.</summary>
    Task UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Removes the entity with the given id. Throws <see cref="APITemplate.Domain.Exceptions.NotFoundException"/> if the entity does not exist.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
