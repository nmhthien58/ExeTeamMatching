using BuildingBlocks.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Validation;

/// <summary>
/// Chạy FluentValidation cho mọi tham số action có validator đăng ký (<c>IValidator&lt;T&gt;</c>).
/// Lỗi → 400 ProblemDetails có <c>errors</c> theo field (camelCase). Controller không phải tự gọi validate.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = ProblemFactory.ToCamelCase(failure.PropertyName);
                if (!errors.TryGetValue(key, out var list))
                {
                    errors[key] = list = [];
                }
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors.ToDictionary(e => e.Key, e => e.Value.Distinct().ToArray()));
        }

        await next();
    }
}
