using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using APITemplate.Application.Common.Errors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace APITemplate.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that executes FluentValidation for every request before its handler runs.
/// </summary>
/// <remarks>
/// How it works:
/// - Runs all registered <see cref="IValidator{T}"/> for the request type (TRequest).
/// - Additionally validates nested complex objects (including items inside enumerable properties),
///   if validators for those runtime types are registered in DI.
/// - Throws <see cref="APITemplate.Domain.Exceptions.ValidationException"/> when any failures are found.
///
/// Why this exists:
/// - Centralizes validation (REST + GraphQL both dispatch via MediatR).
/// - Keeps handlers focused on business logic (commands/queries) instead of validation plumbing.
///
/// Limitations / scope:
/// - Only validates values reachable via public readable instance properties on TRequest.
/// - For collections: validates items that are non-null and non-primitive/value-type (string is excluded).
/// - Does not attempt to traverse arbitrary object graphs recursively (only one "hop" from the request).
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cache of readable public instance properties for each request type.
    // Safe growth: bounded by number of MediatR request types in the application.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadablePublicInstancePropertiesCache = new();

    // Cache of "IEnumerable<IValidator<SomeRuntimeType>>" constructed types used for DI resolution.
    // This avoids repeated reflection for nested object validation.
    private static readonly ConcurrentDictionary<Type, Type> ValidatorsEnumerableTypeCache = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IValidator<TRequest>> _requestValidators;

    public ValidationBehavior(
        IServiceProvider serviceProvider,
        IEnumerable<IValidator<TRequest>> requestValidators)
    {
        // IServiceProvider is used to resolve validators for nested runtime types (object properties / collection items).
        _serviceProvider = serviceProvider;

        // Validators specifically registered for this request type (IValidator<TRequest>).
        _requestValidators = requestValidators;
    }

    /// <summary>
    /// Executes validation before the request handler and throws when validation fails.
    /// </summary>
    /// <param name="request">The MediatR request being processed.</param>
    /// <param name="next">Delegate that invokes the next behavior / the handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler response when validation succeeds.</returns>
    /// <exception cref="APITemplate.Domain.Exceptions.ValidationException">
    /// Thrown when one or more FluentValidation rules fail.
    /// </exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Collect all FluentValidation failures in one list so we can throw a single domain exception.
        var failures = new List<ValidationFailure>();

        // 1) Validate the request itself (all validators for TRequest).
        failures.AddRange(await ValidateAsync(request, _requestValidators, ct));

        // 2) Validate "nested" values reachable from the request (complex properties and complex items in collections).
        foreach (var nestedValue in GetNestedValues(request))
            failures.AddRange(await ValidateNestedAsync(nestedValue, ct));

        // 3) If anything failed, throw a domain validation exception that the API layer can map to a proper HTTP response.
        if (failures.Count > 0)
        {
            throw new Domain.Exceptions.ValidationException(
                string.Join("; ", failures.Select(failure => failure.ErrorMessage).Distinct()),
                ErrorCatalog.General.ValidationFailed);
        }

        // 4) All good → continue to the next behavior / the actual request handler.
        return await next();
    }

    /// <summary>
    /// Runs the given FluentValidation validators against a value and returns all failures.
    /// </summary>
    /// <typeparam name="T">Value type being validated.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validators">Validators to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of validation failures (empty when valid).</returns>
    private static async Task<List<ValidationFailure>> ValidateAsync<T>(
        T value,
        IEnumerable<IValidator<T>> validators,
        CancellationToken ct)
    {
        // Runs a set of validators for a single value and returns all failures.
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            // FluentValidation returns a ValidationResult containing errors (if any).
            var result = await validator.ValidateAsync(value, ct);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        return failures;
    }

    /// <summary>
    /// Validates a nested runtime value by resolving validators for its runtime type from DI.
    /// </summary>
    /// <param name="value">Nested object instance to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of validation failures (empty when no validators exist or the object is valid).</returns>
    private async Task<List<ValidationFailure>> ValidateNestedAsync(object value, CancellationToken ct)
    {
        // We resolve "IEnumerable<IValidator<RuntimeType>>" from DI.
        // Example: for value.GetType() == CreateWidgetRequest → IEnumerable<IValidator<CreateWidgetRequest>>.
        var validatorsType = ValidatorsEnumerableTypeCache.GetOrAdd(
            value.GetType(),
            runtimeType =>
            {
                // Construct IValidator<RuntimeType>.
                var validatorType = typeof(IValidator<>).MakeGenericType(runtimeType);
                // Construct IEnumerable<IValidator<RuntimeType>> for DI resolution.
                return typeof(IEnumerable<>).MakeGenericType(validatorType);
            });

        // Resolve validators for this runtime type. If none are registered, nested validation is skipped.
        var validators = _serviceProvider.GetService(validatorsType) as IEnumerable;

        if (validators is null)
            return [];

        var failures = new List<ValidationFailure>();
        // Non-generic context is enough because we execute validators through IValidator (non-generic).
        var validationContext = new ValidationContext<object>(value);

        foreach (var validator in validators)
        {
            // Defensive: ensure we only run actual FluentValidation validators.
            if (validator is not IValidator nonGenericValidator)
                continue;

            var result = await nonGenericValidator.ValidateAsync(validationContext, ct);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        return failures;
    }

    /// <summary>
    /// Extracts nested complex values from the request that should be validated in addition to the request itself.
    /// </summary>
    /// <remarks>
    /// This is intentionally shallow (one hop):
    /// - yields complex object properties
    /// - yields complex items within enumerable properties
    /// Scalars (value types and strings) are excluded.
    /// </remarks>
    /// <param name="request">The request instance.</param>
    /// <returns>Nested object instances that may have their own validators.</returns>
    private static IEnumerable<object> GetNestedValues(TRequest request)
    {
        // Cache the property list for the request type to avoid repeated reflection per request.
        var properties = ReadablePublicInstancePropertiesCache.GetOrAdd(
            typeof(TRequest),
            requestType =>
                requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .ToArray());

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;

            // Skip scalars. FluentValidation on the root request is responsible for validating scalar fields.
            if (propertyType == typeof(string) || propertyType.IsValueType)
                continue;

            // Read property value once (reflection call).
            var value = property.GetValue(request);
            if (value is null)
                continue;

            // If the property is a collection (but not a string), validate each complex item.
            // Example: List<LineItemRequest> → validate each LineItemRequest instance (if validator exists).
            if (value is IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is null)
                        continue;

                    // Skip scalar collection items (e.g., List<Guid>, List<int>, List<string>).
                    var itemType = item.GetType();
                    if (itemType == typeof(string) || itemType.IsValueType)
                        continue;

                    // Complex object inside a collection → candidate for nested validation.
                    yield return item;
                }

                continue;
            }

            // Complex object property (single nested object) → candidate for nested validation.
            if (!propertyType.IsValueType && propertyType != typeof(string))
                yield return value;
        }
    }
}
