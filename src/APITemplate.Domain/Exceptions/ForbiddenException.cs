namespace APITemplate.Domain.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(
        string message,
        string? errorCode = null)
        : base(message, errorCode)
    {
    }
}
