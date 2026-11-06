using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;

namespace Notification.Worker.Notifications;

public sealed record DeviceRegistration(string Token, string Platform);

public sealed class DeviceTokenService(NotificationDbContext db)
{
    private static readonly HashSet<string> Platforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android", "ios",
    };

    public async Task RegisterAsync(ClaimsPrincipal user, DeviceRegistration request, CancellationToken ct)
    {
        Validate(request);
        var userId = RecipientId(user);
        var token = request.Token.Trim();
        var now = DateTimeOffset.UtcNow;
        var existing = await db.DeviceTokens.SingleOrDefaultAsync(x => x.Token == token, ct);
        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                Platform = request.Platform.Trim().ToLowerInvariant(),
                CreatedAt = now,
                LastSeenAt = now,
            });
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = request.Platform.Trim().ToLowerInvariant();
            existing.LastSeenAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(ClaimsPrincipal user, string token, CancellationToken ct)
    {
        var userIds = RecipientIds(user);
        if (userIds.Length == 0)
            throw new ForbiddenException("device-token-unavailable", "Tài khoản không có vai trò dùng ứng dụng mobile.");

        var decoded = Uri.UnescapeDataString(token);
        var existing = await db.DeviceTokens.SingleOrDefaultAsync(x => x.Token == decoded, ct);
        if (existing is null || !userIds.Contains(existing.UserId)) return;
        db.DeviceTokens.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    private static Guid RecipientId(ClaimsPrincipal user)
    {
        var id = RecipientIds(user).FirstOrDefault();
        return id != Guid.Empty
            ? id
            : throw new ForbiddenException("device-token-unavailable", "Tài khoản không có vai trò dùng ứng dụng mobile.");
    }

    private static Guid[] RecipientIds(ClaimsPrincipal user) =>
        new[] { user.GetStudentId(), user.GetLecturerId(), user.GetMentorId() }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();

    private static void Validate(DeviceRegistration request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Trim().Length > 4096)
            errors["token"] = ["Device token phải từ 1 đến 4096 ký tự."];
        if (string.IsNullOrWhiteSpace(request.Platform) || !Platforms.Contains(request.Platform.Trim()))
            errors["platform"] = ["Nền tảng phải là android hoặc ios."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
}
