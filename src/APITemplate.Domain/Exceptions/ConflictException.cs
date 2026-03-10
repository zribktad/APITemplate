namespace APITemplate.Domain.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(message, errorCode, metadata)
    {
    }
}
