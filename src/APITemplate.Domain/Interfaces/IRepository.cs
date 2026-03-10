using Ardalis.Specification;

namespace APITemplate.Domain.Interfaces;

public interface IRepository<T> : IRepositoryBase<T> where T : class
{
    // Inherited from IRepositoryBase<T> (Ardalis):
    //   GetByIdAsync<TId>(TId id, ct)
    //   ListAsync(ISpecification<T>, ct) → List<T>
    //   ListAsync(ISpecification<T, TResult>, ct) → List<TResult>   ← HTTP path
    //   FirstOrDefaultAsync, CountAsync, AnyAsync, ...
    //   AddAsync(T entity, ct), UpdateAsync(T entity, ct), DeleteAsync(T entity, ct)

    // Ardalis only has DeleteAsync(T entity), we also need DeleteAsync(Guid id)
    Task DeleteAsync(Guid id, CancellationToken ct = default, string? errorCode = null);
}
