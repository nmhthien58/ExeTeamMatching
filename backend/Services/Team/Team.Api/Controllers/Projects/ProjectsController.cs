using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Projects;
using Team.Services.Teams;

namespace Team.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/teams/{teamId:guid}/project")]
public sealed class TeamProjectController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    public Task<ProjectDetailResponse> Get(Guid teamId, CancellationToken ct) => projects.GetForTeamAsync(teamId, ct);
}

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/projects")]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<ProjectDetailResponse>> Search([FromQuery] ProjectQuery query, CancellationToken ct) =>
        projects.SearchAsync(query, ct);

    [HttpGet("unlinked-teams")]
    public Task<IReadOnlyList<TeamCardResponse>> UnlinkedTeams([FromQuery] Guid semesterId, CancellationToken ct) =>
        projects.UnlinkedTeamsAsync(semesterId, ct);

    [HttpPost("{id:guid}/link-team")]
    public Task<ProjectDetailResponse> LinkTeam(Guid id, LinkTeamInput input, CancellationToken ct) =>
        projects.LinkTeamAsync(id, input, User, ct);
}
