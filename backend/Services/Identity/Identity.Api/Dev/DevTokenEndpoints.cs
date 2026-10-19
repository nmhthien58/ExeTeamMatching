using Identity.Services.Dev;

namespace Identity.Api.Dev;

/// <summary>
/// <c>POST /api/dev/token</c> – lấy JWT không cần Google. Program.cs chỉ gọi hai hàm dưới đây khi môi trường là
/// Development; ở môi trường khác route không tồn tại (404).
/// </summary>
public static class DevTokenEndpoints
{
    public static IServiceCollection AddDevTokenIssuer(this IServiceCollection services) =>
        services.AddScoped<DevTokenService>();

    public static IEndpointRouteBuilder MapDevTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/token", async (DevTokenRequest request, DevTokenService dev, HttpContext http, CancellationToken ct) =>
                Results.Ok(await dev.IssueAsync(request, http.Connection.RemoteIpAddress, ct)))
            .AllowAnonymous()
            .WithTags("Dev")
            .WithSummary("CHỈ DEVELOPMENT – cấp JWT cho role/email tùy chọn, không cần Google");
        return app;
    }
}
