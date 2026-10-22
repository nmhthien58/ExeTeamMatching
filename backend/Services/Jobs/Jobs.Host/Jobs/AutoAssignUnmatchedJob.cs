using ExeTeamMatching.Protos.Matching.V1;
using Jobs.Host.Clients;
using Jobs.Host.Configuration;
using Microsoft.Extensions.Options;

namespace Jobs.Host.Jobs;

/// <summary>
/// AutoAssignUnmatchedJob (BE-54, rule 14): sau khi chốt nhóm, xin phương án Matching.PlanAutoAssignment (gRPC) rồi gọi Team áp dụng.
/// Admin chạy lại bằng nút "Chạy lại ghép tự động" ở W20 (Team <c>POST /api/auto-assignment/run</c>) hoặc Trigger trên /hangfire.
/// </summary>
public sealed class AutoAssignUnmatchedJob(
    MatchingService.MatchingServiceClient matching,
    ITeamClient team,
    IOptions<JobsOptions> options,
    ILogger<AutoAssignUnmatchedJob> logger)
{
    public async Task RunAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var rules = options.Value;
        var plan = await matching.PlanAutoAssignmentAsync(new PlanAutoAssignmentRequest
        {
            SemesterId = semesterId.ToString(),
            Course = courseCode == "EXE201" ? Course.Exe201 : Course.Exe101,
            MinSize = rules.MinTeamSize,
            MaxSize = rules.MaxTeamSize,
            MinSpecializations = rules.MinSpecializations,
        }, cancellationToken: ct);

        if (plan.Fills.Count == 0 && plan.NewTeams.Count == 0)
        {
            logger.LogInformation("Ghép tự động {Course}: không có gì để áp dụng, còn {Unassigned} sinh viên", courseCode, plan.UnassignedStudentIds.Count);
            return;
        }

        await team.ApplyAutoAssignmentAsync(new AutoAssignApplyRequest(
            semesterId,
            courseCode,
            plan.Fills.Select(f => new AutoAssignFill(Guid.Parse(f.TeamId), f.StudentIds.Select(Guid.Parse).ToList())).ToList(),
            plan.NewTeams.Select(t => new AutoAssignNewTeam(
                t.SuggestedName, Guid.Parse(t.LeaderStudentId), t.Members.Select(m => Guid.Parse(m.StudentId)).ToList())).ToList()), ct);
        logger.LogInformation("Ghép tự động {Course}: bổ sung {Fills} nhóm, tạo {NewTeams} nhóm, còn {Unassigned} sinh viên cho Admin",
            courseCode, plan.Fills.Count, plan.NewTeams.Count, plan.UnassignedStudentIds.Count);
    }
}
