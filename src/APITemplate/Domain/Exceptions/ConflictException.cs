using APITemplate.Application.Errors;
using Microsoft.AspNetCore.Http;

namespace APITemplate.Domain.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(
        string message,
        string errorCode = ErrorCatalog.General.Conflict,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(message, errorCode, StatusCodes.Status409Conflict, "Conflict", metadata)
    {
    }
}
