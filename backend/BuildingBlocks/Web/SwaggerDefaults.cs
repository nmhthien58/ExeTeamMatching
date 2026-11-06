using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace BuildingBlocks.Web;

public static class SwaggerDefaults
{
    private const string Scheme = "Bearer";

    /// <summary>Swagger có nút Authorize: dán JWT (không kèm chữ "Bearer") để thử API cần đăng nhập.</summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(Scheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT hệ thống (POST /api/auth/google, ở Development có thể lấy qua POST /api/dev/token).",
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = Scheme } }] = [],
            });
        });
}
