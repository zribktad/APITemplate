using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Validation;
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public NotEmptyAttribute() : base("'{0}' is required and must not be empty or whitespace.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var isEmpty = value is null
            || (value is string str && string.IsNullOrWhiteSpace(str))
            || (value is Guid guid && guid == Guid.Empty);

        if (isEmpty)
            return new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                [validationContext.MemberName!]);

        return ValidationResult.Success;
    }
}
