namespace APITemplate.Application.Common.Options;

public sealed class PostgresRetryOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public int MaxRetryDelaySeconds { get; set; } = 5;
}
