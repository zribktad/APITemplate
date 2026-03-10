namespace APITemplate.Domain.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(
        string entityName,
        object id,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(
            $"{entityName} with id '{id}' not found.",
            errorCode,
            metadata)
    {
    }
}
