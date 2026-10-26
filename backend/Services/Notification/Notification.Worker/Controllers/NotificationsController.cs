using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Worker.Notifications;

namespace Notification.Worker.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationPage>> List([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool unreadOnly = false, CancellationToken ct = default) =>
        Ok(await notifications.ListAsync(User, page, pageSize, unreadOnly, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCount>> Unread(CancellationToken ct) =>
        Ok(await notifications.UnreadAsync(User, ct));

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await notifications.ReadAsync(User, id, ct);
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await notifications.ReadAllAsync(User, ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<EmailPreferences>> Preferences(CancellationToken ct) =>
        Ok(await notifications.PreferencesAsync(User, ct));

    [HttpPut("preferences")]
    public async Task<IActionResult> SetPreferences(EmailPreferences request, CancellationToken ct)
    {
        await notifications.SetPreferencesAsync(User, request, ct);
        return NoContent();
    }
}
