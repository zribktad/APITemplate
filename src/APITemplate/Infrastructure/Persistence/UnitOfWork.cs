using APITemplate.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace APITemplate.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private readonly Func<IExecutionStrategy> _executionStrategyFactory;

    public UnitOfWork(AppDbContext dbContext)
        : this(dbContext, () => dbContext.Database.CreateExecutionStrategy())
    {
    }

    internal UnitOfWork(AppDbContext dbContext, Func<IExecutionStrategy> executionStrategyFactory)
    {
        _dbContext = dbContext;
        _executionStrategyFactory = executionStrategyFactory;
    }

    public Task CommitAsync(CancellationToken ct = default)
        => _dbContext.SaveChangesAsync(ct);

    // For multi-step write flows only. Single writes should use repository + CommitAsync.
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = _executionStrategyFactory();
        await strategy.ExecuteAsync(
            state: action,
            operation: async (_, transactionalAction, cancellationToken) =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await transactionalAction();
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            },
            verifySucceeded: null,
            ct);
    }

    // Same as above, but returns a value produced inside the transaction.
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        var strategy = _executionStrategyFactory();
        return await strategy.ExecuteAsync(
            state: action,
            operation: async (_, transactionalAction, cancellationToken) =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await transactionalAction();
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            },
            verifySucceeded: null,
            ct);
    }
}
