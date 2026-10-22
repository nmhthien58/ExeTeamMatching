using BuildingBlocks.Web;
using Hangfire;
using Jobs.Host;
using Jobs.Host.Configuration;
using Microsoft.Extensions.Options;

// Job Service (BE-51) – Hangfire + dashboard /hangfire. Không có API public; gọi nội bộ bằng token dịch vụ.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("jobs", useAuth: false);
builder.Services.AddJobsHost(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults(useAuth: false);
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "EXE Team Matching – Jobs",
    Authorization = [new DashboardAccessFilter(allowRemote: app.Environment.IsDevelopment())],
});
RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>(), app.Services.GetRequiredService<IOptions<JobsOptions>>().Value);

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
