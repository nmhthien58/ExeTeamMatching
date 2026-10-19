using BuildingBlocks.Web;

// Notification Service – khung (BE-04, BE-05). API đọc thông báo + consumer Kafka (BE-40..42).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("notification");
builder.Services.AddApiControllers(typeof(Program).Assembly);

var app = builder.Build();

app.UseServiceDefaults();
app.MapControllers();

app.Run();
