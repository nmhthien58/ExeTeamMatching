using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BuildingBlocks.Observability;

/// <summary>
/// CorrelationId của yêu cầu hiện tại, đi xuyên Gateway → REST → gRPC → Kafka.
/// Lưu bằng <see cref="AsyncLocal{T}"/> nên đọc được cả trong BackgroundService (consumer Kafka, Outbox).
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Tên header gRPC (metadata luôn viết thường).</summary>
    public const string GrpcHeaderName = "x-correlation-id";

    public const string LogProperty = "CorrelationId";

    private static readonly AsyncLocal<string?> Current = new();

    public static string? Id => Current.Value;

    public static string NewId() => Guid.NewGuid().ToString("N")[..16];

    /// <summary>Đặt CorrelationId cho luồng hiện tại và đẩy vào log; dispose để trả lại giá trị cũ.</summary>
    public static IDisposable Begin(string? correlationId)
    {
        var previous = Current.Value;
        Current.Value = string.IsNullOrWhiteSpace(correlationId) ? NewId() : correlationId;
        var logScope = LogContext.PushProperty(LogProperty, Current.Value);
        return new Scope(previous, logScope);
    }

    private sealed class Scope(string? previous, IDisposable logScope) : IDisposable
    {
        public void Dispose()
        {
            logScope.Dispose();
            Current.Value = previous;
        }
    }
}

/// <summary>Đọc / sinh header <c>X-Correlation-Id</c>, trả lại trong response và đặt làm TraceIdentifier.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationContext.HeaderName].FirstOrDefault();
        using (CorrelationContext.Begin(incoming))
        {
            var id = CorrelationContext.Id!;
            context.TraceIdentifier = id;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationContext.HeaderName] = id;
                return Task.CompletedTask;
            });
            await next(context);
        }
    }
}

/// <summary>Gắn CorrelationId vào mọi lời gọi HTTP nội bộ (đăng ký bằng <c>AddHttpMessageHandler</c>).</summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (CorrelationContext.Id is { } id && !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.Add(CorrelationContext.HeaderName, id);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
