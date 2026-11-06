using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Vai trò ghi vào nhật ký (rule 10) theo vai trò của người thực hiện.</summary>
public sealed class UserExtensionsTests
{
    private static ClaimsPrincipal User(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimNames.Subject, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Roles, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimNames.Name, ClaimNames.Roles));
    }

    [Theory]
    [InlineData(new[] { Roles.Student }, Roles.Student)]
    [InlineData(new[] { Roles.Lecturer }, Roles.Lecturer)]
    [InlineData(new[] { Roles.Mentor }, Roles.Mentor)]
    [InlineData(new[] { Roles.Admin }, Roles.Admin)]
    [InlineData(new[] { Roles.Lecturer, Roles.Mentor }, Roles.Lecturer)]
    [InlineData(new[] { Roles.Student, Roles.Admin }, Roles.Admin)]
    public void Role_follows_the_caller(string[] roles, string expected) =>
        Assert.Equal(expected, User(roles).ToAuditActor().Role);

    [Fact]
    public void Lecturer_also_mentor_is_logged_with_the_role_used()
    {
        var user = User(Roles.Lecturer, Roles.Mentor);
        Assert.Equal(Roles.Mentor, user.ToAuditActor(Roles.Mentor).Role);
        Assert.Equal(Roles.Lecturer, user.ToAuditActor(Roles.Lecturer).Role);
    }

    [Fact]
    public void Acting_role_the_caller_does_not_hold_is_ignored() =>
        Assert.Equal(Roles.Lecturer, User(Roles.Lecturer).ToAuditActor(Roles.Admin).Role);
}
