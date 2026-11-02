using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using Yarp.ReverseProxy.Transforms;

// API Gateway (BE-09): YARP định tuyến /api/* tới Identity, Academic, Team, Notification, Collaboration
// và hub /hubs/collab (WebSocket). Kiểm tra JWT ở cổng (trừ /api/auth/*), CORS cho web, gộp Swagger.
// /internal/* và Matching, Job không có route nên không ra được ngoài.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("gateway");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(CorrelationContext.HeaderName)));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context => context.AddRequestTransform(transform =>
    {
        // Service phía sau nhận cùng CorrelationId với Gateway (middleware đã sinh nếu client không gửi).
        transform.ProxyRequest.Headers.Remove(CorrelationContext.HeaderName);
        if (CorrelationContext.Id is { } id)
        {
            transform.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationContext.HeaderName, id);
        }
        return ValueTask.CompletedTask;
    }));

var app = builder.Build();

app.UseCors();
app.UseServiceDefaults();

// Swagger gộp: mỗi service tự phát swagger.json (Development), Gateway proxy qua /swagger-proxy/{service}/.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.EnablePersistAuthorization(); // giữ JWT khi đổi service ở dropdown
        foreach (var service in new[] { "identity", "academic", "team", "notification", "collab" })
        {
            options.SwaggerEndpoint($"/swagger-proxy/{service}/swagger/v1/swagger.json", service);
        }
    });
}

app.MapReverseProxy();

app.Run();
