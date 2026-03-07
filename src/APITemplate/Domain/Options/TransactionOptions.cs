using System.Data;

namespace APITemplate.Domain.Options;

public sealed record TransactionOptions
{
    public IsolationLevel? IsolationLevel { get; init; }
    public int? TimeoutSeconds { get; init; }
    public bool? RetryEnabled { get; init; }
    public int? RetryCount { get; init; }
    public int? RetryDelaySeconds { get; init; }

    public bool IsEmpty()
        => IsolationLevel is null
            && TimeoutSeconds is null
            && RetryEnabled is null
            && RetryCount is null
            && RetryDelaySeconds is null;
}
