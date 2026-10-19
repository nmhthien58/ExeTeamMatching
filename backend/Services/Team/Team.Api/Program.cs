using BuildingBlocks.Web;

// Team Service – cấu hình chung ở BuildingBlocks (BE-05): Serilog + CorrelationId, ProblemDetails, JWT, FluentValidation.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("team");
builder.Services.AddApiControllers(typeof(Program).Assembly);
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
