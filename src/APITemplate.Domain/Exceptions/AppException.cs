namespace APITemplate.Domain.Exceptions;

public abstract class AppException : Exception
{
    public string? ErrorCode { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    protected AppException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null) : base(message)
    {
        ErrorCode = errorCode;
        Metadata = metadata;
    }
}
