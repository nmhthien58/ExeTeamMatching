using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Worker.Notifications;

namespace Notification.Worker.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications/devices")]
public sealed class DeviceTokensController(DeviceTokenService devices) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(DeviceRegistration request, CancellationToken ct)
    {
        await devices.RegisterAsync(User, request, ct);
        return NoContent();
    }

    [HttpDelete("{token}")]
    public async Task<IActionResult> Remove(string token, CancellationToken ct)
    {
        await devices.RemoveAsync(User, token, ct);
        return NoContent();
    }
}
