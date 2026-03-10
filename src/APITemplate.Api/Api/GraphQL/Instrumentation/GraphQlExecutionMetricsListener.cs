using System.Diagnostics;
using APITemplate.Infrastructure.Observability;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Resolvers;

namespace APITemplate.Api.GraphQL.Instrumentation;

public sealed class GraphQlExecutionMetricsListener : ExecutionDiagnosticEventListener
{
    public override IDisposable ExecuteRequest(IRequestContext context)
    {
        var operationType = GetOperationType(context);
        var startedAt = Stopwatch.GetTimestamp();

        return Scope.Create(() =>
        {
            var hasErrors = context.Result is IOperationResult operationResult
                && operationResult.Errors is { Count: > 0 };
            GraphQlTelemetry.RecordRequest(
                operationType,
                hasErrors,
                Stopwatch.GetElapsedTime(startedAt));
        });
    }

    public override void RequestError(IRequestContext context, Exception exception)
        => GraphQlTelemetry.RecordRequestError();

    public override void SyntaxError(IRequestContext context, IError error)
        => GraphQlTelemetry.RecordSyntaxError();

    public override void ValidationErrors(IRequestContext context, IReadOnlyList<IError> errors)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            GraphQlTelemetry.RecordValidationError();
        }
    }

    public override void ResolverError(IMiddlewareContext context, IError error)
        => GraphQlTelemetry.RecordResolverError();

    public override void AddedDocumentToCache(IRequestContext context)
        => GraphQlTelemetry.RecordDocumentCacheMiss();

    public override void RetrievedDocumentFromCache(IRequestContext context)
        => GraphQlTelemetry.RecordDocumentCacheHit();

    public override void AddedOperationToCache(IRequestContext context)
        => GraphQlTelemetry.RecordOperationCacheMiss();

    public override void RetrievedOperationFromCache(IRequestContext context)
        => GraphQlTelemetry.RecordOperationCacheHit();

    public override void OperationCost(IRequestContext context, double fieldCost, double typeCost)
        => GraphQlTelemetry.RecordOperationCost(fieldCost, typeCost);

    private static string GetOperationType(IRequestContext context)
        => context.Operation?.Type.ToString().ToLowerInvariant() ?? TelemetryDefaults.Unknown;

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;

        private Scope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public static Scope Create(Action onDispose) => new(onDispose);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDispose();
            }
        }
    }
}
