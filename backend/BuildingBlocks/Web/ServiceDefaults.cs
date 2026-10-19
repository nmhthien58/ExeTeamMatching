using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Observability;
using BuildingBlocks.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BuildingBlocks.Web;

/// <summary>
/// Cấu hình chung của mọi service (BE-05). Trong Program.cs:
/// <code>
/// builder.AddServiceDefaults("team");
/// builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(SomeValidator).Assembly);
/// var app = builder.Build();
/// app.UseServiceDefaults();
/// app.MapControllers();
/// </code>
/// </summary>
public static class ServiceDefaults
{
    /// <summary>Serilog + CorrelationId + ProblemDetails + JWT (nếu có section <c>Jwt</c>) + health check.</summary>
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName, bool useAuth = true)
    {
        builder.Host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = CorrelationContext.Id ?? context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddTransient<CorrelationIdHandler>();
        builder.Services.AddHealthChecks();

        if (useAuth)
        {
            builder.Services.AddJwtAuth(builder.Configuration);
        }

        return builder;
    }

    /// <summary>Controllers với JSON camelCase, enum dạng chuỗi, FluentValidation tự chạy và lỗi model binding theo cùng format.</summary>
    public static IMvcBuilder AddApiControllers(this IServiceCollection services, params Assembly[] validatorAssemblies)
    {
        foreach (var assembly in validatorAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => ProblemFactory.ToCamelCase(e.Key.TrimStart('$', '.')),
                            e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Giá trị không hợp lệ." : x.ErrorMessage).ToArray());
                    var problem = ProblemFactory.Validation(context.HttpContext, errors, null);
                    return new ObjectResult(problem) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
                };
            });
    }

    /// <summary>Thứ tự middleware chuẩn: CorrelationId → bắt lỗi → log request → xác thực → phân quyền → /health.</summary>
    public static WebApplication UseServiceDefaults(this WebApplication app, bool useAuth = true)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging();

        if (useAuth)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }
}
