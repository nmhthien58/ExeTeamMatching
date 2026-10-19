using System.Net;
using BuildingBlocks.Errors;
using Identity.Repositories.Accounts;
using Identity.Services.Auth;
using Identity.Services.Tokens;

namespace Identity.Services.Dev;

/// <summary>Body của <c>POST /api/dev/token</c>. Chỉ id ứng với <see cref="Role"/> được dùng.</summary>
public sealed record DevTokenRequest(string Role, string? Email, Guid? StudentId, Guid? LecturerId, Guid? MentorId);

/// <summary>
/// CHỈ DÙNG Ở DEVELOPMENT – cấp JWT không cần Google để cả nhóm thử API và giao diện.
/// Tìm tài khoản theo email (mặc định <c>dev.&lt;role&gt;@example.com</c>), chưa có thì tạo ACTIVE; thêm vai trò nếu thiếu
/// (gọi nhiều lần cùng email để có nhiều vai trò, ví dụ Lecturer + Mentor). Không đăng ký service này ngoài Development.
/// </summary>
public sealed class DevTokenService(IUserAccountRepository accounts, TokenIssuer issuer, TimeProvider clock)
{
    public async Task<TokenResponse> IssueAsync(DevTokenRequest request, IPAddress? ip, CancellationToken ct)
    {
        var roleId = RoleIds.FromCode(request.Role) ?? throw new RequestValidationException(
            new Dictionary<string, string[]> { ["role"] = ["role phải là Student, Lecturer, Mentor hoặc Admin."] });
        var roleCode = RoleIds.ToCode(roleId);
        var email = string.IsNullOrWhiteSpace(request.Email) ? $"dev.{roleCode.ToLowerInvariant()}@example.com" : request.Email.Trim();

        var account = await accounts.FindByEmailAsync(email, ct);
        if (account is null)
        {
            account = new UserAccount { Id = Guid.NewGuid(), Email = email, FullName = $"Dev {roleCode}", Status = AccountStatuses.Active };
            accounts.Add(account);
        }

        var requestedRef = roleId switch
        {
            RoleIds.Student => request.StudentId,
            RoleIds.Lecturer => request.LecturerId,
            RoleIds.Mentor => request.MentorId,
            _ => null,
        };
        var role = account.Roles.FirstOrDefault(r => r.RoleId == roleId);
        if (RoleIds.NeedsExternalRef(roleId))
        {
            var externalRef = requestedRef ?? role?.ExternalRefId ?? Guid.NewGuid();
            if (await accounts.ExternalRefInUseAsync(roleId, externalRef, account.Id, ct))
            {
                throw new ConflictException("external-ref-in-use", "Id đã thuộc tài khoản khác", $"{roleCode} {externalRef} đã gắn với một email khác.");
            }
            if (role is null)
            {
                account.Roles.Add(new UserAccountRole { UserAccountId = account.Id, RoleId = roleId, ExternalRefId = externalRef });
            }
            else
            {
                role.ExternalRefId = externalRef;
            }
        }
        else if (role is null)
        {
            account.Roles.Add(new UserAccountRole { UserAccountId = account.Id, RoleId = roleId });
        }

        if (account.Status == AccountStatuses.Pending)
        {
            account.Status = AccountStatuses.Active;
        }
        account.LastLoginAt = clock.GetUtcNow();

        var (response, _) = issuer.Issue(account, ip, isFirstLogin: false);
        await accounts.SaveChangesAsync(ct);
        return response;
    }
}
