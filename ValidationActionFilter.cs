using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

/// <summary>
/// Validates any action argument that has a registered IValidator&lt;T&gt;, before
/// the controller action runs. Registered globally in Program.cs.
///
/// This is NOT the deprecated FluentValidation.AspNetCore auto-validation
/// package - that package hooks into MVC's synchronous ModelState pipeline
/// and has been deprecated by FluentValidation's own maintainers (see
/// https://github.com/FluentValidation/FluentValidation/issues/1960). This
/// filter is a small, explicit, fully-async alternative: it only does what's
/// written here, and async validators (e.g. ones that check the database)
/// work correctly with it.
/// </summary>
public class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<FieldError>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                errors.AddRange(result.Errors.Select(e => new FieldError
                {
                    Field = e.PropertyName,
                    Message = e.ErrorMessage,
                }));
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ErrorResponse
            {
                Error = "ValidationError",
                Message = "One or more fields are invalid",
                Details = errors,
            });
            return;
        }

        await next();
    }
}
