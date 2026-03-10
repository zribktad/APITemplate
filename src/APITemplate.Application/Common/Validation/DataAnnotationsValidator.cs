using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;
public abstract class DataAnnotationsValidator<T> : AbstractValidator<T> where T : class
{
    protected DataAnnotationsValidator()
    {
        RuleFor(x => x).Custom(static (model, context) =>
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

            // For records, also validate constructor parameter attributes that may not be on properties.
            ValidateConstructorParameterAttributes(model, results);

            foreach (var result in results)
                context.AddFailure(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage!);
        });
    }

    private static void ValidateConstructorParameterAttributes(T model, List<ValidationResult> results)
    {
        var type = model.GetType();
        var constructor = type.GetConstructors().FirstOrDefault();
        if (constructor is null)
            return;

        var existingMembers = new HashSet<string>(results.SelectMany(r => r.MemberNames));

        foreach (var parameter in constructor.GetParameters())
        {
            if (existingMembers.Contains(parameter.Name ?? string.Empty))
                continue;

            var validationAttributes = parameter.GetCustomAttributes<ValidationAttribute>();
            var property = type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
                continue;

            var value = property.GetValue(model);
            var validationContext = new ValidationContext(model) { MemberName = parameter.Name };

            foreach (var attribute in validationAttributes)
            {
                var result = attribute.GetValidationResult(value, validationContext);
                if (result != ValidationResult.Success && result is not null)
                    results.Add(result);
            }
        }
    }
}
