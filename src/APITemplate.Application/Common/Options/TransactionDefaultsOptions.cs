using System.Data;
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
    {
        var resolved = new TransactionOptions
        {
            IsolationLevel = overrides?.IsolationLevel ?? IsolationLevel,
            TimeoutSeconds = overrides?.TimeoutSeconds ?? TimeoutSeconds,
            RetryEnabled = overrides?.RetryEnabled ?? RetryEnabled,
            RetryCount = overrides?.RetryCount ?? RetryCount,
            RetryDelaySeconds = overrides?.RetryDelaySeconds ?? RetryDelaySeconds
        };

        ValidateNonNegative(resolved.TimeoutSeconds, nameof(TransactionOptions.TimeoutSeconds));
        ValidateNonNegative(resolved.RetryCount, nameof(TransactionOptions.RetryCount));
        ValidateNonNegative(resolved.RetryDelaySeconds, nameof(TransactionOptions.RetryDelaySeconds));

        return resolved;
    }

    private static void ValidateNonNegative(int? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} cannot be negative.");
        }
    }
}
