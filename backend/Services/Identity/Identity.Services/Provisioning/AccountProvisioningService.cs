using Identity.Repositories.Accounts;
using Microsoft.Extensions.Logging;

namespace Identity.Services.Provisioning;

/// <summary>Phần payload Identity cần từ topic <c>exe.student</c> (docs/contracts/events.md).</summary>
public sealed record StudentImported(Guid StudentId, string FullName, string Email, DateOnly SemesterEndDate);

public sealed record LecturerRegistered(Guid LecturerId, string FullName, string Email, bool IsActive);

/// <summary>
/// Academic phát lại ở mọi lần sửa mentor. <see cref="LecturerId"/> khác null = giảng viên kiêm mentor: dùng chung tài khoản giảng viên
/// (rule 20), email do sự kiện của giảng viên quyết định.
/// </summary>
public sealed record MentorRegistered(Guid MentorId, string FullName, string Email, Guid? LecturerId, bool IsActive);

/// <summary>
/// Tạo sẵn tài khoản từ danh sách import (BE-13, BE-64, rule 5): lúc đăng nhập Identity chỉ tra <see cref="UserAccount"/>, không gọi Academic.
/// Sự kiện lặp lại chỉ cập nhật: tìm theo id Academic trước (email có thể đã đổi), rồi tới email.
/// <para>
/// Khóa một vai trò (<c>isActive = false</c>): nếu đó là vai trò duy nhất thì khóa tài khoản và giữ vai trò để còn tìm theo id;
/// nếu tài khoản còn vai trò khác thì chỉ bỏ vai trò đó. Mở lại một vai trò trên tài khoản đang khóa: các vai trò còn lại
/// đều là vai trò đã khóa nên bị bỏ, tài khoản mở lại. Nhờ vậy LecturerRegistered và MentorRegistered của cùng một người
/// đến theo thứ tự nào cũng ra cùng kết quả.
/// </para>
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
        SetRoleActive(account, RoleIds.Lecturer, e.IsActive);
        await accounts.SaveChangesAsync(ct);
    }

    /// <summary>
    /// BE-64. Mentor ngoài trường: tài khoản riêng theo email, khóa mentor thì khóa tài khoản. Giảng viên kiêm mentor
    /// (<see cref="MentorRegistered.LecturerId"/>): thêm role Mentor vào tài khoản của giảng viên, khóa mentor chỉ bỏ role Mentor.
    /// Không đổi email của tài khoản giảng viên – LecturerRegistered làm việc đó – nên giảng viên đổi email thì hai sự kiện
    /// đến theo thứ tự nào cũng chỉ có một tài khoản.
    /// </summary>
    public async Task HandleAsync(MentorRegistered e, CancellationToken ct)
    {
        var email = e.Email.Trim();
        var holder = await accounts.FindByExternalRefAsync(RoleIds.Mentor, e.MentorId, ct);

        UserAccount? target;
        if (e.LecturerId is { } lecturerId)
        {
            target = await accounts.FindByExternalRefAsync(RoleIds.Lecturer, lecturerId, ct) ?? await accounts.FindByEmailAsync(email, ct);
        }
        else if (holder is not null && holder.Roles.All(r => r.RoleId == RoleIds.Mentor))
        {
            target = holder;
            await MoveEmailAsync(target, email, ct);
        }
        else
        {
            // Chưa có tài khoản, hoặc mentor vừa thôi gắn với giảng viên: tài khoản riêng theo email của mentor.
            target = await accounts.FindByEmailAsync(email, ct);
        }

        if (holder is not null && !ReferenceEquals(holder, target))
        {
            // Role Mentor chuyển sang tài khoản khác: bỏ ở tài khoản cũ trước (uq_user_account_roles_external_ref).
            holder.Roles.RemoveAll(r => r.RoleId == RoleIds.Mentor);
            if (holder.Roles.Count == 0)
            {
                holder.Status = AccountStatuses.Locked;
            }
            await accounts.SaveChangesAsync(ct);
            logger.LogInformation("Chuyển role Mentor {MentorId} khỏi tài khoản {AccountId}", e.MentorId, holder.Id);
        }

        if (target is null)
        {
            if (e.LecturerId is not null && !e.IsActive)
            {
                return; // Mentor đã khóa của giảng viên chưa có tài khoản: không có gì để cấp.
            }
            target = new UserAccount { Id = Guid.NewGuid(), Email = email, FullName = e.FullName.Trim(), Status = AccountStatuses.Pending };
            accounts.Add(target);
            logger.LogInformation("Tạo sẵn tài khoản {AccountId} ({Role})", target.Id, RoleIds.ToCode(RoleIds.Mentor));
        }

        if (target.Roles.All(r => r.RoleId != RoleIds.Lecturer))
        {
            target.FullName = e.FullName.Trim(); // tên của giảng viên do LecturerRegistered quyết định
        }
        SetRole(target, RoleIds.Mentor, e.MentorId);
        target.AllowedUntil = null;
        SetRoleActive(target, RoleIds.Mentor, e.IsActive);
        await accounts.SaveChangesAsync(ct);
    }

    private async Task<UserAccount> UpsertAsync(short roleId, Guid externalRefId, string rawEmail, string fullName, CancellationToken ct)
    {
        var email = rawEmail.Trim();
        var account = await accounts.FindByExternalRefAsync(roleId, externalRefId, ct);
        if (account is not null)
        {
            await MoveEmailAsync(account, email, ct);
        }

        account ??= await accounts.FindByEmailAsync(email, ct);
        if (account is null)
        {
            account = new UserAccount { Id = Guid.NewGuid(), Email = email, FullName = fullName, Status = AccountStatuses.Pending };
            accounts.Add(account);
            logger.LogInformation("Tạo sẵn tài khoản {AccountId} ({Role})", account.Id, RoleIds.ToCode(roleId));
        }

        account.FullName = fullName;
        SetRole(account, roleId, externalRefId);
        return account;
    }

    private async Task MoveEmailAsync(UserAccount account, string email, CancellationToken ct)
    {
        if (string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (await accounts.FindByEmailAsync(email, ct) is not null)
        {
            // Hai tài khoản cùng một người: để sự kiện vào DLQ cho người xử lý tay, không tự gộp.
            throw new InvalidOperationException($"Email mới của tài khoản {account.Id} đã thuộc tài khoản khác.");
        }
        account.Email = email;
        account.GoogleSubject = null; // email mới đăng nhập bằng tài khoản Google khác
    }

    private static void SetRole(UserAccount account, short roleId, Guid externalRefId)
    {
        var role = account.Roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role is null)
        {
            account.Roles.Add(new UserAccountRole { UserAccountId = account.Id, RoleId = roleId, ExternalRefId = externalRefId });
        }
        else
        {
            role.ExternalRefId = externalRefId;
        }
    }

    /// <summary>Khóa / mở một vai trò theo quy tắc ở đầu lớp.</summary>
    private void SetRoleActive(UserAccount account, short roleId, bool isActive)
    {
        if (!isActive)
        {
            if (account.Roles.All(r => r.RoleId == roleId))
            {
                account.Status = AccountStatuses.Locked;
            }
            else
            {
                account.Roles.RemoveAll(r => r.RoleId == roleId);
                logger.LogInformation("Bỏ role {Role} của tài khoản {AccountId}, giữ các vai trò khác", RoleIds.ToCode(roleId), account.Id);
            }
            return;
        }

        if (account.Status == AccountStatuses.Locked)
        {
            account.Roles.RemoveAll(r => r.RoleId != roleId);
            account.Status = account.GoogleSubject is null ? AccountStatuses.Pending : AccountStatuses.Active;
        }
    }
}
