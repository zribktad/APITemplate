using Microsoft.AspNetCore.Http;

namespace APITemplate.Domain.Exceptions;

public sealed class ValidationException : AppException
{
    public ValidationException(
        string message,
        string errorCode = ErrorCatalog.General.ValidationFailed,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(message, errorCode, StatusCodes.Status400BadRequest, "Bad Request", metadata)
    {
    }
}
