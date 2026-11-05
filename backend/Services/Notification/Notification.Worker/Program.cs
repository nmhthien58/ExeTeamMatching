using BuildingBlocks.Web;
using BuildingBlocks.Messaging;
using Notification.Worker.Data;
using Notification.Worker.Events;
using Notification.Worker.Notifications;
using Notification.Worker.Email;
using Notification.Worker.Push;
using Microsoft.EntityFrameworkCore;

// Notification Service – khung (BE-04, BE-05). API đọc thông báo + consumer Kafka (BE-40..42).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("notification");
builder.Services.AddApiControllers(typeof(Program).Assembly);
builder.Services.AddDbContext<NotificationDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddKafka(builder.Configuration, "notification");
builder.Services.AddScoped<IProcessedEventStore, NotificationProcessedEventStore>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DeviceTokenService>();
builder.Services.AddScoped<FcmPushDispatcher>();
builder.Services.AddHttpClient<IFcmSender, FirebaseFcmSender>();
builder.Services.AddHostedService<SmtpEmailWorker>();
foreach (var (topic, eventType) in NotificationEventHandler.Subscriptions)
{
    builder.Services.AddEventHandler<NotificationEventHandler>(topic, eventType);
}

var app = builder.Build();

app.UseServiceDefaults();
app.MapControllers();

app.Run();
