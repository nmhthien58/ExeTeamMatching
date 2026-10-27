using Collab.Repositories.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collab.Services.Messages;

public static class MessagesModule
{
    /// <summary>Module Messages (BE-69): kênh Nhóm / Cố vấn, tin nhắn, luồng, đã đọc, câu hỏi cho cố vấn. Cần <c>AddFilesModule</c> (IWorkspaceLookup).</summary>
    public static IServiceCollection AddMessagesModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageService, MessageService>();
        return services;
    }
}
