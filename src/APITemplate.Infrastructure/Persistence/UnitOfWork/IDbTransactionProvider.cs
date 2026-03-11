using System.Data;
using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore.Storage;

namespace APITemplate.Infrastructure.Persistence;

public interface IDbTransactionProvider
{
    IDbContextTransaction? CurrentTransaction { get; }
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct);
    IExecutionStrategy CreateExecutionStrategy(TransactionOptions options);
}
