using BuildingBlocks.Web;
using Team.Services;

// Team Service – cấu hình chung ở BuildingBlocks (BE-05): Serilog + CorrelationId, ProblemDetails, JWT, FluentValidation.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("team");
builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(ServiceCollectionExtensions).Assembly);
builder.Services.AddTeamServices(builder.Configuration);
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

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
