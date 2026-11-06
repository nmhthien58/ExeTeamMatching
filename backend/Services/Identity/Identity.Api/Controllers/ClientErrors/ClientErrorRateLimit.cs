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

    /// <summary>
    /// Số proxy tin cậy nối địa chỉ vào cuối <c>X-Forwarded-For</c> (CR-05). Gateway luôn nối 1; sau Caddy hoặc
    /// Railway thì 2. Phần đầu header do client tự gửi nên không dùng làm khóa.
    /// </summary>
    public int TrustedProxyHops { get; set; } = 1;
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
                ClientKey(context, options.TrustedProxyHops),
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
    /// Identity chỉ nhận request từ Gateway nên địa chỉ kết nối luôn là Gateway. Mỗi proxy tin cậy nối địa chỉ nó thấy
    /// vào cuối X-Forwarded-For, nên IP thật là phần tử thứ <paramref name="trustedHops"/> tính từ cuối. Lấy phần tử đầu
    /// thì client tự đặt header là đổi được khóa và vượt giới hạn (CR-05, MB-59).
    /// </summary>
    public static string ClientKey(HttpContext context, int trustedHops = 1)
    {
        var parts = context.Request.Headers[ForwardedForHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = parts.Length == 0 ? null : parts[Math.Max(0, parts.Length - Math.Max(1, trustedHops))];
        return IPAddress.TryParse(candidate, out var address)
            ? address.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;
    }
}
