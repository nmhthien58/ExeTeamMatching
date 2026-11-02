using Identity.Repositories.Accounts;
using Microsoft.Extensions.Logging;

namespace Identity.Services.Provisioning;

/// <summary>Phần payload Identity cần từ topic <c>exe.student</c> (docs/contracts/events.md).</summary>
public sealed record StudentImported(Guid StudentId, string FullName, string Email, DateOnly SemesterEndDate);

public sealed record LecturerRegistered(Guid LecturerId, string FullName, string Email, bool IsActive);

/// <summary>
/// Tạo sẵn tài khoản từ danh sách import (BE-13, rule 5): lúc đăng nhập Identity chỉ tra <see cref="UserAccount"/>, không gọi Academic.
/// Sự kiện lặp lại chỉ cập nhật: tìm theo id Academic trước (email có thể đã đổi), rồi tới email.
/// </summary>
public sealed class AccountProvisioningService(IUserAccountRepository accounts, ILogger<AccountProvisioningService> logger)
{
    public async Task HandleAsync(StudentImported e, CancellationToken ct)
    {
        var account = await UpsertAsync(RoleIds.Student, e.StudentId, e.Email, e.FullName, ct);

        // Tài khoản có vai trò khác không hết hạn theo học kỳ. Import lại kỳ cũ không rút ngắn hạn đang có.
        if (account.Roles.All(r => r.RoleId == RoleIds.Student))
        {
            var until = new DateTimeOffset(e.SemesterEndDate.AddDays(1), TimeOnly.MinValue, TimeSpan.Zero);
            account.AllowedUntil = account.AllowedUntil > until ? account.AllowedUntil : until;
        }
        await accounts.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(LecturerRegistered e, CancellationToken ct)
    {
        var account = await UpsertAsync(RoleIds.Lecturer, e.LecturerId, e.Email, e.FullName, ct);
        account.AllowedUntil = null;

        if (account.Roles.All(r => r.RoleId == RoleIds.Lecturer))
        {
            if (!e.IsActive)
            {
                account.Status = AccountStatuses.Locked;
            }
            else if (account.Status == AccountStatuses.Locked)
            {
                account.Status = account.GoogleSubject is null ? AccountStatuses.Pending : AccountStatuses.Active;
            }
        }
        else if (!e.IsActive)
        {
            logger.LogWarning("Không khóa tài khoản {AccountId}: còn vai trò khác ngoài Lecturer", account.Id);
        }
        await accounts.SaveChangesAsync(ct);
    }

    private async Task<UserAccount> UpsertAsync(short roleId, Guid externalRefId, string rawEmail, string fullName, CancellationToken ct)
    {
        var email = rawEmail.Trim();
        var account = await accounts.FindByExternalRefAsync(roleId, externalRefId, ct);
        if (account is not null && !string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            if (await accounts.FindByEmailAsync(email, ct) is not null)
            {
                // Hai tài khoản cùng một người: để sự kiện vào DLQ cho người xử lý tay, không tự gộp.
                throw new InvalidOperationException($"Email mới của tài khoản {account.Id} đã thuộc tài khoản khác.");
            }
            account.Email = email;
            account.GoogleSubject = null; // email mới đăng nhập bằng tài khoản Google khác
        }

        account ??= await accounts.FindByEmailAsync(email, ct);
        if (account is null)
        {
            account = new UserAccount { Id = Guid.NewGuid(), Email = email, FullName = fullName, Status = AccountStatuses.Pending };
            accounts.Add(account);
            logger.LogInformation("Tạo sẵn tài khoản {AccountId} ({Role})", account.Id, RoleIds.ToCode(roleId));
        }

        account.FullName = fullName;
        var role = account.Roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role is null)
        {
            account.Roles.Add(new UserAccountRole { UserAccountId = account.Id, RoleId = roleId, ExternalRefId = externalRefId });
        }
        else
        {
            role.ExternalRefId = externalRefId;
        }
        return account;
    }
}
