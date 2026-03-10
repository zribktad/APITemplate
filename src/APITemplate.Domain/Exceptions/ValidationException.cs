namespace APITemplate.Domain.Exceptions;

public sealed class ValidationException : AppException
{
    public ValidationException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(message, errorCode, metadata)
    {
    }
}
