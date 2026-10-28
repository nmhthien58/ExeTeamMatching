using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Worker.Notifications;

namespace Notification.Worker.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/announcements")]
public sealed class AnnouncementsController(NotificationService notifications) : ControllerBase
{
    /// <summary>
    /// Bộ môn gửi thông báo chung (W23) – chỉ Admin.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AnnouncementResponse>> Create(
        [FromBody] AnnouncementRequest request,
        CancellationToken ct)
    {
        var result = await notifications.SendAnnouncementAsync(request, ct);
        return Accepted(result);
    }
}
