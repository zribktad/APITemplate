using APITemplate.Application.Common.Errors;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;

public static class FluentValidationExtensions
{
    public static async Task ValidateAndThrowAppAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken ct = default,
        string? errorCode = null)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
            throw new Domain.Exceptions.ValidationException(
                string.Join("; ", result.Errors.Select(e => e.ErrorMessage)),
                errorCode ?? ErrorCatalog.General.ValidationFailed);
    }
}
