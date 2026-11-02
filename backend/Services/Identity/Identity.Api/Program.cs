using System.Text.Json.Serialization;
using BuildingBlocks.Web;
using Identity.Api.Dev;
using Identity.Services;

// Identity Service – cấu hình chung ở BuildingBlocks (BE-05): Serilog + CorrelationId, ProblemDetails, JWT, FluentValidation.
// Đăng nhập Google → JWT hệ thống, refresh, logout (BE-10 → BE-12).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("identity");
builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(ServiceCollectionExtensions).Assembly);
builder.Services.AddIdentityServices(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevTokenIssuer();
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Lấy JWT không cần Google – chỉ đăng ký ở Development (docs/setup/google-sign-in.md mục 6).
if (app.Environment.IsDevelopment())
{
    app.MapDevTokenEndpoints();
}

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
