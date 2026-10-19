using BuildingBlocks.Web;
using Collab.Services;

// Collaboration Service (BE-62): workspace nhóm – tin nhắn, việc, file, họp, checkpoint.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("collab");
builder.Services.AddApiControllers(typeof(Program).Assembly, typeof(ServiceCollectionExtensions).Assembly);
builder.Services.AddCollabServices(builder.Configuration);
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
