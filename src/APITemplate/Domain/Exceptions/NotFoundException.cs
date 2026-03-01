namespace APITemplate.Domain.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' not found.") { }
}
