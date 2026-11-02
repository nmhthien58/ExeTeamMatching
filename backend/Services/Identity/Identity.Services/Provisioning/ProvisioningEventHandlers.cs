using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Services.Provisioning;

/// <summary>Một handler cho mỗi sự kiện tạo sẵn tài khoản; đọc payload rồi chuyển cho <see cref="AccountProvisioningService"/>.</summary>
public sealed class ProvisioningEventHandler<TPayload>(AccountProvisioningService service) : IIntegrationEventHandler
{
    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken) =>
        envelope.GetPayload<TPayload>() switch
        {
            StudentImported e => service.HandleAsync(e, cancellationToken),
            LecturerRegistered e => service.HandleAsync(e, cancellationToken),
            _ => throw new NotSupportedException(typeof(TPayload).Name),
        };
}

public static class ProvisioningEventRegistration
{
    /// <summary>Consumer exe.student (BE-13).</summary>
    public static IServiceCollection AddProvisioningConsumers(this IServiceCollection services) => services
        .AddEventHandler<ProvisioningEventHandler<StudentImported>>(Topics.Student, nameof(StudentImported))
        .AddEventHandler<ProvisioningEventHandler<LecturerRegistered>>(Topics.Student, nameof(LecturerRegistered));
}
