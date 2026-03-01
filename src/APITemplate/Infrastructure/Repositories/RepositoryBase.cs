using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

// Generic base repository — T is constrained to class (reference type) so EF Core can track it.
// abstract = cannot be instantiated directly, must be inherited (e.g. ProductRepository : RepositoryBase<Product>).
// virtual methods allow derived repos to override individual operations when needed.
// SaveChangesAsync is intentionally NOT called here — use IUnitOfWork.CommitAsync() in the service layer.
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    // Shared DbContext injected via constructor — all derived repos use the same instance per request (scoped DI lifetime).
    protected readonly AppDbContext DbContext;

    protected RepositoryBase(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    // FindAsync hits the EF Identity Map first (in-memory cache of tracked entities),
    // so if T was already loaded in this request, no DB query is issued.
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Params array syntax [id] required by EF Core 8 overload that accepts CancellationToken alongside keys.
        return await DbContext.Set<T>().FindAsync([id], ct);
    }

    public virtual IQueryable<T> AsQueryable()
    {
        return DbContext.Set<T>().AsNoTracking();
    }

    public virtual Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        // Add stages the entity with EntityState.Added — no DB hit yet.
        // SaveChangesAsync must be called via IUnitOfWork.CommitAsync() after staging all changes.
        DbContext.Set<T>().Add(entity);
        return Task.FromResult(entity);
    }

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        // Update marks every scalar property as Modified — issues a full UPDATE even for unchanged columns.
        // For partial updates consider Attach + explicit property marking instead.
        DbContext.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<T>().FindAsync([id], ct);

        if (entity is null)
            throw new NotFoundException(typeof(T).Name, id);

        // Remove sets EntityState.Deleted; SaveChangesAsync (via IUnitOfWork) translates it to a DELETE statement.
        DbContext.Set<T>().Remove(entity);
    }
}
