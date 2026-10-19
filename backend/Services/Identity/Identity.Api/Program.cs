using BuildingBlocks.Web;
using Identity.Services;

// Identity Service – cấu hình chung ở BuildingBlocks (BE-05): Serilog + CorrelationId, ProblemDetails, JWT, FluentValidation.
// Đăng nhập Google → JWT hệ thống, refresh, logout (BE-10 → BE-12).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("identity");
builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(ServiceCollectionExtensions).Assembly);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
