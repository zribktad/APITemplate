using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace APITemplate.Infrastructure.Persistence;

internal static class UnitOfWorkExecutionStrategyFactory
{
    public static IExecutionStrategy Create(DbContext dbContext, TransactionOptions effectiveOptions)
    {
        if (effectiveOptions.RetryEnabled == false)
            return new NonRetryingExecutionStrategy(dbContext);

        if (!dbContext.Database.IsNpgsql())
            return dbContext.Database.CreateExecutionStrategy();

        return new NpgsqlRetryingExecutionStrategy(
            dbContext,
            effectiveOptions.RetryCount ?? 3,
            TimeSpan.FromSeconds(effectiveOptions.RetryDelaySeconds ?? 5),
            errorCodesToAdd: null);
    }
}
