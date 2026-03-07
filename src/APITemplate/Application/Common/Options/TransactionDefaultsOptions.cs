using System.Data;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;

namespace APITemplate.Application.Common.Options;

public sealed class TransactionDefaultsOptions
{
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
    public int TimeoutSeconds { get; set; } = 30;
    public bool RetryEnabled { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;

    internal TransactionOptions Resolve(TransactionOptions? overrides)
        => new()
        {
            IsolationLevel = overrides?.IsolationLevel ?? IsolationLevel,
            TimeoutSeconds = overrides?.TimeoutSeconds ?? TimeoutSeconds,
            RetryEnabled = overrides?.RetryEnabled ?? RetryEnabled,
            RetryCount = overrides?.RetryCount ?? RetryCount,
            RetryDelaySeconds = overrides?.RetryDelaySeconds ?? RetryDelaySeconds
        };
}
