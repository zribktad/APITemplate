using Microsoft.AspNetCore.Http;

namespace APITemplate.Domain.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entityName, object id, string errorCode = ErrorCatalog.General.NotFound)
        : base(
            $"{entityName} with id '{id}' not found.",
            errorCode,
            StatusCodes.Status404NotFound,
            "Not Found")
    {
    }
}
