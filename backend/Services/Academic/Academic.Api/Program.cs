using Academic.Services;
using BuildingBlocks.Web;

// Academic Service – cấu hình chung ở BuildingBlocks (BE-05): Serilog + CorrelationId, ProblemDetails, JWT, FluentValidation.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("academic");
builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(ServiceCollectionExtensions).Assembly);
builder.Services.AddAcademicServices(builder.Configuration);
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
