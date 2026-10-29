using FluentValidation;

namespace ClinicBooking.Client.Validation;

/// <summary>Adapts a FluentValidation validator to MudForm's per-field <c>Validation</c> parameter.</summary>
public static class MudValidation
{
    public static Func<object, string, Task<IEnumerable<string>>> For<T>(IValidator<T> validator) =>
        async (model, propertyName) =>
        {
            var context = ValidationContext<T>.CreateWithOptions((T)model, options => options.IncludeProperties(propertyName));
            var result = await validator.ValidateAsync(context);
            return result.IsValid ? [] : result.Errors.Select(e => e.ErrorMessage);
        };
}
