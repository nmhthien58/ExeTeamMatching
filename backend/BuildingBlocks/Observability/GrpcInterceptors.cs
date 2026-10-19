using BuildingBlocks.Errors;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BuildingBlocks.Observability;

/// <summary>
/// Phía server gRPC (Matching): đọc <c>x-correlation-id</c> từ metadata và đổi <see cref="AppException"/>
/// thành <see cref="RpcException"/> với status tương ứng. Đăng ký: <c>AddGrpc(o =&gt; o.Interceptors.Add&lt;GrpcServerInterceptor&gt;())</c>.
/// </summary>
public sealed class GrpcServerInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var incoming = context.RequestHeaders.GetValue(CorrelationContext.GrpcHeaderName);
        using (CorrelationContext.Begin(incoming))
        {
            try
            {
                return await continuation(request, context);
            }
            catch (AppException ex)
            {
                throw new RpcException(new Status(ToStatusCode(ex.Status), $"{ex.Code}: {ex.Detail ?? ex.Title}"));
            }
        }
    }

    private static StatusCode ToStatusCode(int httpStatus) => httpStatus switch
    {
        400 => StatusCode.InvalidArgument,
        401 => StatusCode.Unauthenticated,
        403 => StatusCode.PermissionDenied,
        404 => StatusCode.NotFound,
        409 => StatusCode.FailedPrecondition,
        _ => StatusCode.Internal,
    };
}

/// <summary>
/// Phía client gRPC (Team, Job gọi Matching): gắn CorrelationId vào metadata.
/// Đăng ký: <c>AddGrpcClient&lt;…&gt;().AddInterceptor&lt;GrpcClientCorrelationInterceptor&gt;()</c>.
/// </summary>
public sealed class GrpcClientCorrelationInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        if (CorrelationContext.Id is { } id && headers.Get(CorrelationContext.GrpcHeaderName) is null)
        {
            headers.Add(CorrelationContext.GrpcHeaderName, id);
        }
        var options = context.Options.WithHeaders(headers);
        return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options));
    }
}
