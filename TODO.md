# TODO

## Validation Unification

- [ ] Migrate pure FluentValidation validators to data annotation attributes where possible, using `DataAnnotationsValidator<T>` as the base class. For cross-field rules that cannot be expressed via attributes (e.g. `CreatedTo >= CreatedFrom`, conditional `Description` requirement), combine both approaches: attributes for simple single-field rules + FluentValidation for cross-field/conditional logic.
