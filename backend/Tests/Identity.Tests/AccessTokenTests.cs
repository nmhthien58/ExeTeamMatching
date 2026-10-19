using System.Text;
using System.Text.Json;
using BuildingBlocks.Auth;
using Identity.Repositories.Accounts;
using Identity.Services.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Tests;

/// <summary>JWT do Identity phát có đủ claim theo docs/contracts/README.md và kiểm tra được bằng cùng khóa.</summary>
public sealed class AccessTokenTests
{
    private static readonly JwtOptions Options = new() { SigningKey = IdentityApiFactory.SigningKey, AccessTokenMinutes = 15 };

    [Fact]
    public async Task Token_has_sub_email_name_roles_list_and_external_ids()
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = "gv.a@example.com",
            FullName = "Giảng viên A",
            Roles =
            [
                new UserAccountRole { RoleId = RoleIds.Mentor, ExternalRefId = mentorId },
                new UserAccountRole { RoleId = RoleIds.Lecturer, ExternalRefId = lecturerId },
            ],
        };
        var now = DateTimeOffset.UtcNow;

        var (token, expiresAt) = new AccessTokenFactory(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System).Create(account);

        Assert.InRange(expiresAt, now.AddMinutes(14), now.AddMinutes(16));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
        });
        Assert.True(result.IsValid, result.Exception?.Message);

        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
        Assert.Equal(account.Id.ToString(), jwt.GetClaim(ClaimNames.Subject).Value);
        Assert.Equal(account.Email, jwt.GetClaim(ClaimNames.Email).Value);
        Assert.Equal(account.FullName, jwt.GetClaim(ClaimNames.Name).Value);
        Assert.Equal(lecturerId.ToString(), jwt.GetClaim(ClaimNames.LecturerId).Value);
        Assert.Equal(mentorId.ToString(), jwt.GetClaim(ClaimNames.MentorId).Value);
        Assert.False(jwt.TryGetClaim(ClaimNames.StudentId, out _));
        Assert.Equal(new[] { Roles.Lecturer, Roles.Mentor }, jwt.Claims.Where(c => c.Type == ClaimNames.Roles).Select(c => c.Value));
    }

    [Fact]
    public void Single_role_is_still_a_json_array()
    {
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = "sv@example.com",
            FullName = "Sinh viên",
            Roles = [new UserAccountRole { RoleId = RoleIds.Student, ExternalRefId = Guid.NewGuid() }],
        };

        var (token, _) = new AccessTokenFactory(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System).Create(account);

        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(new JsonWebToken(token).EncodedPayload));
        var roles = payload.RootElement.GetProperty(ClaimNames.Roles);
        Assert.Equal(JsonValueKind.Array, roles.ValueKind);
        Assert.Equal(Roles.Student, roles[0].GetString());
        Assert.Equal(account.Roles[0].ExternalRefId.ToString(), payload.RootElement.GetProperty(ClaimNames.StudentId).GetString());
    }
}
