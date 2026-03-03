using System.ComponentModel.DataAnnotations;
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

            foreach (var result in results)
                context.AddFailure(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage!);
        });
    }
}
