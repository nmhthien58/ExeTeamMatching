using System.Text;
using BuildingBlocks.Auth;
using Identity.Repositories.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Services.Tokens;

/// <summary>
/// Ký access token HS256 theo <see cref="JwtOptions"/>. Claim: sub, email, name, roles (luôn là mảng),
/// student_id / lecturer_id / mentor_id lấy từ <c>user_account_roles.external_ref_id</c>.
/// </summary>
public sealed class AccessTokenFactory(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Create(UserAccount account)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = account.Id.ToString(),
            [ClaimNames.Email] = account.Email,
            [ClaimNames.Name] = account.FullName,
            [ClaimNames.Roles] = RoleCodes(account).ToArray(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };
        foreach (var role in account.Roles.Where(r => r.ExternalRefId is not null))
        {
            if (ExternalRefClaim(role.RoleId) is { } claim)
            {
                claims[claim] = role.ExternalRefId!.Value.ToString();
            }
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
        });
        return (token, expiresAt);
    }

    /// <summary>Mã vai trò theo thứ tự id (Student, Lecturer, Admin, Mentor).</summary>
    public static IEnumerable<string> RoleCodes(UserAccount account) =>
        account.Roles.OrderBy(r => r.RoleId).Select(r => RoleIds.ToCode(r.RoleId));

    public static Guid? ExternalRef(UserAccount account, short roleId) =>
        account.Roles.FirstOrDefault(r => r.RoleId == roleId)?.ExternalRefId;

    private static string? ExternalRefClaim(short roleId) => roleId switch
    {
        RoleIds.Student => ClaimNames.StudentId,
        RoleIds.Lecturer => ClaimNames.LecturerId,
        RoleIds.Mentor => ClaimNames.MentorId,
        _ => null,
    };
}
