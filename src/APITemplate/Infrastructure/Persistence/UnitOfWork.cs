using System.Data;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/> backed by <see cref="AppDbContext"/>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private readonly TransactionDefaultsOptions _transactionDefaults;
    private readonly Func<TransactionOptions, IExecutionStrategy> _executionStrategyFactory;
    private readonly Func<IDbContextTransaction?> _currentTransactionAccessor;
    private readonly Func<IsolationLevel, CancellationToken, Task<IDbContextTransaction>> _beginTransactionAsync;
    private int _savepointCounter;
    private TransactionOptions? _activeTransactionOptions;

    /// <summary>
    /// Creates a <see cref="UnitOfWork"/> with the default template transaction settings.
    /// Intended primarily for direct construction in tests or narrow infrastructure scenarios.
    /// </summary>
    /// <param name="dbContext">EF Core context that tracks staged relational changes for the current scope.</param>
    public UnitOfWork(AppDbContext dbContext)
        : this(dbContext, Options.Create(new TransactionDefaultsOptions()))
    {
    }

    /// <summary>
    /// Creates a <see cref="UnitOfWork"/> that uses configured transaction defaults for explicit transactions.
    /// </summary>
    /// <param name="dbContext">EF Core context that tracks staged relational changes for the current scope.</param>
    /// <param name="transactionDefaults">
    /// Configured defaults used to resolve the effective isolation level, timeout, and retry policy
    /// for outermost <see cref="ExecuteInTransactionAsync"/> calls.
    /// </param>
    public UnitOfWork(AppDbContext dbContext, IOptions<TransactionDefaultsOptions> transactionDefaults)
        : this(
            dbContext,
            transactionDefaults.Value,
            options => CreateExecutionStrategy(dbContext, options),
            () => dbContext.Database.CurrentTransaction,
            dbContext.Database.BeginTransactionAsync)
    {
    }

    internal UnitOfWork(
        AppDbContext dbContext,
        TransactionDefaultsOptions transactionDefaults,
        Func<TransactionOptions, IExecutionStrategy> executionStrategyFactory,
        Func<IDbContextTransaction?> currentTransactionAccessor,
        Func<IsolationLevel, CancellationToken, Task<IDbContextTransaction>> beginTransactionAsync)
    {
        _dbContext = dbContext;
        _transactionDefaults = transactionDefaults;
        _executionStrategyFactory = executionStrategyFactory;
        _currentTransactionAccessor = currentTransactionAccessor;
        _beginTransactionAsync = beginTransactionAsync;
    }

    /// <summary>
    /// Persists all currently staged relational changes without opening an explicit transaction boundary.
    /// Use this for simple service flows that already know when the write should be flushed.
    /// </summary>
    /// <param name="ct">Cancellation token for the underlying <c>SaveChangesAsync</c> call.</param>
    /// <returns>A task that completes when all staged changes have been flushed to the database.</returns>
    public Task CommitAsync(CancellationToken ct = default)
        => _dbContext.SaveChangesAsync(ct);

    /// <summary>
    /// Executes a write delegate inside an explicit relational transaction.
    /// The outermost call owns transaction creation, retry strategy, timeout application, save, and commit.
    /// </summary>
    /// <param name="action">
    /// Delegate that stages repository/entity changes inside the transaction boundary.
    /// The delegate should not call <see cref="CommitAsync"/>.
    /// </param>
    /// <param name="ct">Cancellation token propagated to transaction, savepoint, and save operations.</param>
    /// <param name="options">
    /// Optional per-call transaction overrides. Non-null values override configured defaults only for the outermost call.
    /// Nested calls inherit the already active outer transaction policy.
    /// </param>
    /// <returns>A task that completes when the transactional delegate has been saved and committed.</returns>
    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken ct = default,
        TransactionOptions? options = null)
        => await ExecuteInTransactionAsync(
            async () =>
            {
                await action();
                return true;
            },
            ct,
            options);

    /// <summary>
    /// Executes a write delegate inside an explicit relational transaction and returns a value created by that flow.
    /// Per-call <paramref name="options"/> override configured defaults only for the outermost transaction boundary.
    /// </summary>
    /// <remarks>
    /// Do not call <see cref="CommitAsync"/> inside <paramref name="action"/>. The outermost transaction saves and commits
    /// after the delegate completes successfully.
    /// </remarks>
    /// <typeparam name="T">Type returned by the transactional delegate.</typeparam>
    /// <param name="action">
    /// Delegate that stages repository/entity changes and returns a value computed inside the transaction boundary.
    /// </param>
    /// <param name="ct">Cancellation token propagated to transaction, savepoint, and save operations.</param>
    /// <param name="options">
    /// Optional per-call transaction overrides. Non-null values override configured defaults only for the outermost call.
    /// Nested calls inherit the already active outer transaction policy.
    /// </param>
    /// <returns>The value returned by <paramref name="action"/> after the transaction has been saved and committed.</returns>
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null)
    {
        var currentTransaction = _currentTransactionAccessor();
        if (currentTransaction is not null)
            return await ExecuteWithinSavepointAsync(currentTransaction, action, options, ct);

        var effectiveOptions = _transactionDefaults.Resolve(options);
        return await ExecuteAsOutermostTransactionAsync(action, effectiveOptions, ct);
    }

    /// <summary>
    /// Executes a nested transaction scope by using a savepoint inside the active outer transaction.
    /// Only null/default nested options are allowed unless they resolve to the same effective outer policy.
    /// </summary>
    /// <typeparam name="T">Type returned by the nested delegate.</typeparam>
    /// <param name="transaction">Currently active outer database transaction.</param>
    /// <param name="action">Nested delegate executed under a savepoint inside <paramref name="transaction"/>.</param>
    /// <param name="options">
    /// Optional nested overrides. Conflicting values are rejected because nested scopes cannot redefine
    /// the already active outer transaction policy.
    /// </param>
    /// <param name="ct">Cancellation token propagated to savepoint operations.</param>
    /// <returns>The value returned by <paramref name="action"/> when the nested scope succeeds.</returns>
    private async Task<T> ExecuteWithinSavepointAsync<T>(
        IDbContextTransaction transaction,
        Func<Task<T>> action,
        TransactionOptions? options,
        CancellationToken ct)
    {
        ValidateNestedTransactionOptions(options);
        var savepointName = $"uow_sp_{Interlocked.Increment(ref _savepointCounter)}";
        var snapshot = CaptureTrackedState();

        // Nested work reuses the active transaction and isolates rollback via a savepoint.
        await transaction.CreateSavepointAsync(savepointName, ct);
        try
        {
            var result = await action();
            await ReleaseSavepointIfSupportedAsync(transaction, savepointName, ct);
            return result;
        }
        catch
        {
            await transaction.RollbackToSavepointAsync(savepointName, ct);
            RestoreTrackedState(snapshot);
            throw;
        }
    }

    /// <summary>
    /// Executes the outermost transaction boundary through EF Core's execution strategy so the whole unit
    /// of work can be replayed on transient relational failures.
    /// </summary>
    /// <typeparam name="T">Type returned by the transactional delegate.</typeparam>
    /// <param name="action">Delegate that stages all relational changes for the outer transaction.</param>
    /// <param name="effectiveOptions">Resolved transaction policy after config defaults and per-call overrides are merged.</param>
    /// <param name="ct">Cancellation token propagated to strategy, transaction, and save operations.</param>
    /// <returns>The value returned by <paramref name="action"/> after the transaction commits successfully.</returns>
    private async Task<T> ExecuteAsOutermostTransactionAsync<T>(
        Func<Task<T>> action,
        TransactionOptions effectiveOptions,
        CancellationToken ct)
    {
        var strategy = _executionStrategyFactory(effectiveOptions);
        var previousActiveOptions = _activeTransactionOptions;
        var previousTimeout = _dbContext.Database.GetCommandTimeout();

        return await strategy.ExecuteAsync(
            state: action,
            operation: async (_, transactionalAction, cancellationToken) =>
            {
                _activeTransactionOptions = effectiveOptions;
                _dbContext.Database.SetCommandTimeout(effectiveOptions.TimeoutSeconds);

                await using var transaction = await _beginTransactionAsync(
                    effectiveOptions.IsolationLevel!.Value,
                    cancellationToken);
                var snapshot = CaptureTrackedState();

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
                    RestoreTrackedState(snapshot);
                    throw;
                }
                finally
                {
                    // Timeout and active policy are scoped to the outermost transaction boundary.
                    _dbContext.Database.SetCommandTimeout(previousTimeout);
                    _activeTransactionOptions = previousActiveOptions;
                }
            },
            verifySucceeded: null,
            ct);
    }

    /// <summary>
    /// Ensures nested transaction scopes inherit the effective outer transaction policy.
    /// </summary>
    /// <param name="options">Optional nested transaction overrides.</param>
    private void ValidateNestedTransactionOptions(TransactionOptions? options)
    {
        if (_activeTransactionOptions is null)
            throw new InvalidOperationException("Nested transaction execution requires an active outer transaction policy.");

        if (options is null || options.IsEmpty())
            return;

        var effectiveOptions = _transactionDefaults.Resolve(options);
        if (effectiveOptions != _activeTransactionOptions)
        {
            throw new InvalidOperationException(
                "Nested transactions inherit the active outer transaction options. " +
                "Pass null/default options inside nested ExecuteInTransactionAsync calls.");
        }
    }

    /// <summary>
    /// Releases the current savepoint when the provider supports explicit savepoint release.
    /// Some providers treat savepoint release as optional, so unsupported cases are ignored.
    /// </summary>
    /// <param name="transaction">Active database transaction that owns the savepoint.</param>
    /// <param name="savepointName">Provider-specific name of the savepoint to release.</param>
    /// <param name="ct">Cancellation token propagated to the provider.</param>
    private async Task ReleaseSavepointIfSupportedAsync(
        IDbContextTransaction transaction,
        string savepointName,
        CancellationToken ct)
    {
        try
        {
            await transaction.ReleaseSavepointAsync(savepointName, ct);
        }
        catch (NotSupportedException)
        {
        }
    }

    /// <summary>
    /// Captures the tracked entity state before entering a transaction/savepoint boundary so EF state can be restored
    /// after rollback. This prevents failed staged changes from leaking into later saves on the same context.
    /// </summary>
    /// <returns>
    /// Snapshot of tracked entities keyed by reference identity, including entity state plus current/original values.
    /// </returns>
    private IReadOnlyDictionary<object, TrackedEntitySnapshot> CaptureTrackedState()
    {
        return _dbContext.ChangeTracker
            .Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .ToDictionary(
                entry => entry.Entity,
                entry => new TrackedEntitySnapshot(
                    entry.State,
                    entry.CurrentValues.Clone(),
                    entry.OriginalValues.Clone()),
                ReferenceEqualityComparer.Instance);
    }

    /// <summary>
    /// Restores tracked entity state captured before a failed transaction/savepoint.
    /// Entities introduced after the snapshot are detached; existing tracked entities return to their previous values/state.
    /// </summary>
    /// <param name="snapshot">Previously captured EF tracking snapshot for the current context.</param>
    private void RestoreTrackedState(IReadOnlyDictionary<object, TrackedEntitySnapshot> snapshot)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
        {
            if (!snapshot.TryGetValue(entry.Entity, out var entitySnapshot))
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(entitySnapshot.CurrentValues);
            entry.OriginalValues.SetValues(entitySnapshot.OriginalValues);
            entry.State = entitySnapshot.State;
        }
    }

    private sealed record TrackedEntitySnapshot(
        EntityState State,
        PropertyValues CurrentValues,
        PropertyValues OriginalValues);

    /// <summary>
    /// Builds the EF Core execution strategy for the effective transaction policy.
    /// Retry-enabled transactions use Npgsql's retrying strategy; disabled retries fall back to a non-retrying strategy.
    /// </summary>
    /// <param name="dbContext">Context used by the execution strategy.</param>
    /// <param name="effectiveOptions">Resolved transaction policy for the outermost transaction boundary.</param>
    /// <returns>An execution strategy matching the effective retry policy.</returns>
    private static IExecutionStrategy CreateExecutionStrategy(
        DbContext dbContext,
        TransactionOptions effectiveOptions)
    {
        if (effectiveOptions.RetryEnabled == false)
            return new NonRetryingExecutionStrategy(dbContext);

        return new NpgsqlRetryingExecutionStrategy(
            dbContext,
            effectiveOptions.RetryCount ?? 3,
            TimeSpan.FromSeconds(effectiveOptions.RetryDelaySeconds ?? 5),
            errorCodesToAdd: null);
    }
}
