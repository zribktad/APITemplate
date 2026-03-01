namespace APITemplate.Domain.Exceptions;

public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }
}
