using BuildingBlocks.Web;

// Job Service – khung (BE-04, BE-05). Hangfire + dashboard /hangfire (BE-51).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("jobs", useAuth: false);

var app = builder.Build();

app.UseServiceDefaults(useAuth: false);

app.Run();
