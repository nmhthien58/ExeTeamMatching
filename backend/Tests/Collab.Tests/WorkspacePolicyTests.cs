using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Collab.Repositories.Workspaces;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Collab.Tests;

/// <summary>BE-14 – policy WorkspaceMember / WorkspaceWriter / WorkspaceAdvisor.</summary>
public sealed class WorkspacePolicyTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Theory]
    [InlineData(WorkspaceRoles.Leader, WorkspaceAccessLevel.Member, true)]
    [InlineData(WorkspaceRoles.Member, WorkspaceAccessLevel.Writer, true)]
    [InlineData(WorkspaceAccess.AdminRole, WorkspaceAccessLevel.Member, true)]
    [InlineData(WorkspaceRoles.Instructor, WorkspaceAccessLevel.Advisor, true)]
    [InlineData(WorkspaceRoles.Mentor, WorkspaceAccessLevel.Advisor, true)]
    [InlineData(WorkspaceRoles.Mentor, WorkspaceAccessLevel.Writer, true)]
    [InlineData(WorkspaceAccess.AdminRole, WorkspaceAccessLevel.Writer, false)]
    [InlineData(WorkspaceAccess.AdminRole, WorkspaceAccessLevel.Advisor, false)]
    [InlineData(WorkspaceRoles.Leader, WorkspaceAccessLevel.Advisor, false)]
    public async Task Level_decides_who_passes(string role, WorkspaceAccessLevel level, bool allowed)
    {
        var (handler, http) = Create(new WorkspaceAccess(WorkspaceId, role, "STUDENT", Guid.NewGuid()));
        var context = Context(level);

        if (allowed)
        {
            await handler.HandleAsync(context);
            Assert.True(context.HasSucceeded);
            Assert.Equal(role, http.GetWorkspaceAccess().Role);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(context));
            Assert.Equal(level == WorkspaceAccessLevel.Writer ? "workspace-read-only" : "not-workspace-advisor", error.Code);
        }
    }

    [Fact]
    public async Task Outsider_gets_not_workspace_member()
    {
        var (handler, _) = Create(null);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(Context(WorkspaceAccessLevel.Member)));
        Assert.Equal("not-workspace-member", error.Code);
    }

    [Fact]
    public async Task Route_without_workspace_id_does_not_succeed()
    {
        var (handler, http) = Create(new WorkspaceAccess(WorkspaceId, WorkspaceRoles.Leader, "STUDENT", Guid.NewGuid()));
        http.Request.RouteValues.Clear();
        var context = Context(WorkspaceAccessLevel.Member);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static (WorkspaceMemberHandler, HttpContext) Create(WorkspaceAccess? access)
    {
        var http = new DefaultHttpContext();
        http.Request.RouteValues["workspaceId"] = WorkspaceId.ToString();
        var handler = new WorkspaceMemberHandler(new HttpContextAccessor { HttpContext = http }, new FakeAccess(access));
        return (handler, http);
    }

    private static AuthorizationHandlerContext Context(WorkspaceAccessLevel level)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimNames.Subject, Guid.NewGuid().ToString())], "test"));
        return new AuthorizationHandlerContext([new WorkspaceMemberRequirement(level)], user, null);
    }

    private sealed class FakeAccess(WorkspaceAccess? access) : IWorkspaceAccessService
    {
        public Task<WorkspaceAccess> RequireAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct) =>
            access is null
                ? throw new ForbiddenException("not-workspace-member", "Bạn không thuộc dự án này")
                : Task.FromResult(access);

        public IReadOnlyCollection<(string Type, Guid Id)> PeopleOf(ClaimsPrincipal user) => [];
    }
}
