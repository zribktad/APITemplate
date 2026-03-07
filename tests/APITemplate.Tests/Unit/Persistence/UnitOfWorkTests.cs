using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class UnitOfWorkTests
{
    [Fact]
    public async Task ExecuteInTransactionAsync_UsesExecutionStrategy_AndPersistsChanges()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var sut = new UnitOfWork(dbContext, () => executionStrategy);

        await sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = "Books"
            });

            await Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncOfT_UsesExecutionStrategy_AndReturnsResult()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var sut = new UnitOfWork(dbContext, () => executionStrategy);

        var createdId = await sut.ExecuteInTransactionAsync(async () =>
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Games"
            };

            dbContext.Categories.Add(category);
            await Task.CompletedTask;
            return category.Id;
        }, TestContext.Current.CancellationToken);

        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        (await dbContext.Categories.SingleAsync(c => c.Id == createdId, TestContext.Current.CancellationToken)).Name.ShouldBe("Games");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionThrows_RollsBackAndPropagates()
    {
        var executionStrategy = new RecordingExecutionStrategy();
        await using var dbContext = CreateDbContext();
        var sut = new UnitOfWork(dbContext, () => executionStrategy);

        var act = () => sut.ExecuteInTransactionAsync(async () =>
        {
            dbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = "Music"
            });

            await Task.CompletedTask;
            throw new InvalidOperationException("boom");
        }, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(act);
        executionStrategy.ExecuteAsyncCallCount.ShouldBe(1);
        (await dbContext.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, new TestTenantProvider(), new TestActorProvider());
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }

    private sealed class RecordingExecutionStrategy : IExecutionStrategy
    {
        public int ExecuteAsyncCallCount { get; private set; }
        public bool RetriesOnFailure => true;

        public void Execute(Action operation)
            => throw new NotSupportedException();

        public TResult Execute<TResult>(Func<TResult> operation)
            => throw new NotSupportedException();

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => throw new NotSupportedException();

        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncCallCount++;
            return operation(cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncCallCount++;
            return operation(cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
