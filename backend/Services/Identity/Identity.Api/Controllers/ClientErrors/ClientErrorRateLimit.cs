using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using BuildingBlocks.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers.ClientErrors;

/// <summary>Cấu hình section <c>ClientErrors:RateLimit</c>.</summary>
public sealed class ClientErrorRateLimitOptions
{
    public const string SectionName = "ClientErrors:RateLimit";

    /// <summary>Số báo cáo mỗi IP được gửi trong một cửa sổ.</summary>
    public int PermitLimit { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>Giới hạn <c>POST /api/client-errors</c> theo IP vì endpoint không cần đăng nhập.</summary>
public static class ClientErrorRateLimit
{
    public const string PolicyName = "client-errors";

    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddClientErrorRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ClientErrorRateLimitOptions.SectionName).Get<ClientErrorRateLimitOptions>()
            ?? new ClientErrorRateLimitOptions();

        return services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = 0,
                }));
            limiter.OnRejected = async (rejected, ct) =>
            {
                var http = rejected.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = options.WindowSeconds.ToString(CultureInfo.InvariantCulture);
                var problem = ProblemFactory.Create(http, StatusCodes.Status429TooManyRequests, "rate-limited",
                    "Gửi quá nhiều báo cáo lỗi", "Thiết bị đã gửi quá nhiều báo cáo. Vui lòng thử lại sau.");
                await http.Response.WriteAsJsonAsync(problem, problem.GetType(), (JsonSerializerOptions?)null, "application/problem+json", ct);
            };
        });
    }

    /// <summary>
    /// Identity chỉ nhận request từ Gateway nên địa chỉ kết nối luôn là Gateway. IP thật là phần tử đầu của
    /// X-Forwarded-For: Caddy ghi đè header client gửi, Gateway nối thêm (route <c>identity-client-errors</c>).
    /// </summary>
    public static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers[ForwardedForHeader].ToString().Split(',', 2)[0].Trim();
        return IPAddress.TryParse(forwarded, out var address)
            ? address.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;
    }
}
