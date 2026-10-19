using BuildingBlocks.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Errors;

/// <summary>Mọi lỗi chưa bắt → ProblemDetails (RFC 7807), content type <c>application/problem+json</c>.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string ErrorTypeBase = "https://exe-team-matching/errors/";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestValidationException v => ProblemFactory.Validation(httpContext, v.Errors, v.Detail),
            FluentValidation.ValidationException v => ProblemFactory.Validation(httpContext, ToDictionary(v), null),
            AppException app => ProblemFactory.Create(httpContext, app.Status, app.Code, app.Title, app.Detail),
            UnauthorizedAccessException => ProblemFactory.Create(httpContext, 401, "unauthorized", "Chưa đăng nhập", null),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested =>
                ProblemFactory.Create(httpContext, 499, "client-closed", "Yêu cầu đã bị hủy", null),
            _ => ProblemFactory.Create(httpContext, 500, "internal", "Lỗi hệ thống", "Đã có lỗi xảy ra. Vui lòng thử lại sau."),
        };

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Lỗi chưa xử lý tại {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Trả lỗi {Status} {Type} tại {Path}", problem.Status, problem.Type, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? 500;
        // Ghi theo kiểu thật để ValidationProblemDetails giữ trường errors.
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        return true;
    }

    private static Dictionary<string, string[]> ToDictionary(FluentValidation.ValidationException v) =>
        v.Errors
            .GroupBy(e => ProblemFactory.ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}

/// <summary>Tạo ProblemDetails thống nhất cho cả exception handler, validate filter và lỗi model binding.</summary>
public static class ProblemFactory
{
    public static ProblemDetails Create(HttpContext context, int status, string code, string title, string? detail)
    {
        var problem = new ProblemDetails
        {
            Type = GlobalExceptionHandler.ErrorTypeBase + code,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = CorrelationContext.Id ?? context.TraceIdentifier;
        return problem;
    }

    public static ValidationProblemDetails Validation(HttpContext context, IDictionary<string, string[]> errors, string? detail)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Type = GlobalExceptionHandler.ErrorTypeBase + "validation",
            Title = "Dữ liệu không hợp lệ",
            Status = 400,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = CorrelationContext.Id ?? context.TraceIdentifier;
        return problem;
    }

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }
        return string.Join('.', name.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
    }
}
