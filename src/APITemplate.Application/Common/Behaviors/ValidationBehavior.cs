using System.Collections;
using System.Reflection;
using APITemplate.Application.Common.Errors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace APITemplate.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IValidator<TRequest>> _requestValidators;

    public ValidationBehavior(
        IServiceProvider serviceProvider,
        IEnumerable<IValidator<TRequest>> requestValidators)
    {
        _serviceProvider = serviceProvider;
        _requestValidators = requestValidators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        failures.AddRange(await ValidateAsync(request, _requestValidators, ct));

        foreach (var nestedValue in GetNestedValues(request))
            failures.AddRange(await ValidateNestedAsync(nestedValue, ct));

        if (failures.Count > 0)
        {
            throw new Domain.Exceptions.ValidationException(
                string.Join("; ", failures.Select(failure => failure.ErrorMessage).Distinct()),
                ErrorCatalog.General.ValidationFailed);
        }

        return await next();
    }

    private static async Task<List<ValidationFailure>> ValidateAsync<T>(
        T value,
        IEnumerable<IValidator<T>> validators,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(value, ct);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        return failures;
    }

    private async Task<List<ValidationFailure>> ValidateNestedAsync(object value, CancellationToken ct)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
        var validatorsType = typeof(IEnumerable<>).MakeGenericType(validatorType);
        var validators = _serviceProvider.GetService(validatorsType) as IEnumerable;

        if (validators is null)
            return [];

        var failures = new List<ValidationFailure>();
        var validationContext = new ValidationContext<object>(value);

        foreach (var validator in validators)
        {
            if (validator is not IValidator nonGenericValidator)
                continue;

            var result = await nonGenericValidator.ValidateAsync(validationContext, ct);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        return failures;
    }

    private static IEnumerable<object> GetNestedValues(TRequest request)
    {
        foreach (var property in typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var propertyType = property.PropertyType;
            if (propertyType == typeof(string) || propertyType.IsValueType)
                continue;

            if (typeof(IEnumerable).IsAssignableFrom(propertyType))
                continue;

            var value = property.GetValue(request);
            if (value is not null)
                yield return value;
        }
    }
}
