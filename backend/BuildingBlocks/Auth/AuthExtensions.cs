using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// JWT Bearer + các policy dùng chung. Hub SignalR nhận token qua query <c>access_token</c>
    /// (chỉ với đường dẫn bắt đầu bằng <c>/hubs/</c>).
    /// </summary>
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey phải có ít nhất 32 ký tự (đặt qua biến môi trường Jwt__SigningKey).");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Roles,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Student, p => p.RequireRole(Roles.Student))
            .AddPolicy(Policies.Lecturer, p => p.RequireRole(Roles.Lecturer))
            .AddPolicy(Policies.Mentor, p => p.RequireRole(Roles.Mentor))
            .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.Advisor, p => p.RequireRole(Roles.Lecturer, Roles.Mentor))
            .AddPolicy(Policies.Internal, p => p.RequireRole(Roles.Service));

        return services;
    }
}

/// <summary>Đọc nhanh các claim của người dùng hiện tại.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimNames.Subject), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token không có claim sub hợp lệ.");

    public static Guid? GetStudentId(this ClaimsPrincipal user) => ParseGuid(user, ClaimNames.StudentId);

    public static Guid? GetLecturerId(this ClaimsPrincipal user) => ParseGuid(user, ClaimNames.LecturerId);

    public static Guid? GetMentorId(this ClaimsPrincipal user) => ParseGuid(user, ClaimNames.MentorId);

    public static string? GetEmail(this ClaimsPrincipal user) => user.FindFirstValue(ClaimNames.Email);

    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal user) =>
        user.FindAll(ClaimNames.Roles).Select(c => c.Value).ToList();

    private static Guid? ParseGuid(ClaimsPrincipal user, string claim) =>
        Guid.TryParse(user.FindFirstValue(claim), out var id) ? id : null;
}
