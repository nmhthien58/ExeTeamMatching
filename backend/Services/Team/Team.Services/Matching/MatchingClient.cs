using BuildingBlocks.Errors;
using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Core;
using Team.Services.Teams;

namespace Team.Services.Matching;

/// <summary>Kết quả ValidateComposition (rule 1, 2); <see cref="Violations"/> là mã TOO_FEW_MEMBERS | TOO_MANY_MEMBERS | NOT_ENOUGH_SPECIALIZATIONS.</summary>
public sealed record CompositionCheck(bool IsValid, IReadOnlyList<string> Violations, IReadOnlyList<string> Messages);

public sealed record PlannedFill(Guid TeamId, IReadOnlyList<Guid> StudentIds);

public sealed record PlannedTeam(string Name, Guid LeaderStudentId, IReadOnlyList<Guid> StudentIds);

/// <summary>Phương án PlanAutoAssignment (rule 14) – Matching chỉ tính, Team áp dụng.</summary>
public sealed record AutoAssignPlan(IReadOnlyList<PlannedFill> Fills, IReadOnlyList<PlannedTeam> NewTeams, IReadOnlyList<Guid> UnassignedStudentIds)
{
    /// <summary>Mọi sinh viên chưa có nhóm mà Matching biết: người được bù, người vào nhóm mới và người còn lại.</summary>
    public IEnumerable<Guid> AllStudentIds =>
        Fills.SelectMany(f => f.StudentIds).Concat(NewTeams.SelectMany(t => t.StudentIds)).Concat(UnassignedStudentIds).Distinct();
}

/// <summary>Gợi ý thành viên (S18): <see cref="Skills"/> là kỹ năng đã tự đánh giá, mạnh trước, ví dụ "Thiết kế UI/UX 4/5".</summary>
public sealed record SuggestedMember(Guid StudentId, int SpecializationId, double Score, IReadOnlyList<string> Reasons, IReadOnlyList<string> Skills);

public sealed record SuggestedMembers(IReadOnlyList<int> MissingSpecializationIds, IReadOnlyList<SuggestedMember> Items);

public sealed record SuggestedTeam(Guid TeamId, double Score, IReadOnlyList<string> Reasons);

/// <summary>Lời gọi gRPC tới Matching Service (PROJECT_CONTEXT §5.5). Matching lỗi hoặc quá hạn → 503 matching-unavailable.</summary>
public interface IMatchingClient
{
    Task<CompositionCheck> ValidateCompositionAsync(IReadOnlyCollection<(Guid StudentId, int SpecializationId)> members, CancellationToken ct);

    Task<AutoAssignPlan> PlanAutoAssignmentAsync(Guid semesterId, string courseCode, CancellationToken ct);

    Task<SuggestedMembers> SuggestMembersAsync(Guid semesterId, string courseCode, Guid teamId, int limit, CancellationToken ct);

    Task<IReadOnlyList<SuggestedTeam>> SuggestTeamsAsync(Guid semesterId, string courseCode, Guid studentId, int limit, CancellationToken ct);
}

public sealed class MatchingClient(MatchingService.MatchingServiceClient grpc) : IMatchingClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public Task<CompositionCheck> ValidateCompositionAsync(IReadOnlyCollection<(Guid StudentId, int SpecializationId)> members, CancellationToken ct) =>
        CallAsync(async options =>
        {
            var request = new ValidateCompositionRequest
            {
                MinSize = TeamRules.MinSize, MaxSize = TeamRules.MaxSize, MinSpecializations = TeamRules.MinSpecializations,
            };
            request.Members.AddRange(members.Select(m => new MemberInfo { StudentId = m.StudentId.ToString(), SpecializationId = m.SpecializationId }));
            var response = await grpc.ValidateCompositionAsync(request, options);
            return new CompositionCheck(response.IsValid, response.Violations.Select(v => v.Code).ToList(), response.Violations.Select(v => v.Message).ToList());
        }, ct);

    public Task<AutoAssignPlan> PlanAutoAssignmentAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        CallAsync(async options =>
        {
            var response = await grpc.PlanAutoAssignmentAsync(new PlanAutoAssignmentRequest
            {
                SemesterId = semesterId.ToString(), Course = ToCourse(courseCode),
                MinSize = TeamRules.MinSize, MaxSize = TeamRules.MaxSize, MinSpecializations = TeamRules.MinSpecializations,
            }, options);
            return new AutoAssignPlan(
                response.Fills.Select(f => new PlannedFill(Guid.Parse(f.TeamId), f.StudentIds.Select(Guid.Parse).ToList())).ToList(),
                response.NewTeams.Select(t => new PlannedTeam(t.SuggestedName, Guid.Parse(t.LeaderStudentId),
                    t.Members.Select(m => Guid.Parse(m.StudentId)).ToList())).ToList(),
                response.UnassignedStudentIds.Select(Guid.Parse).ToList());
        }, ct);

    public Task<SuggestedMembers> SuggestMembersAsync(Guid semesterId, string courseCode, Guid teamId, int limit, CancellationToken ct) =>
        CallAsync(async options =>
        {
            var response = await grpc.SuggestMembersAsync(new SuggestMembersRequest
            {
                SemesterId = semesterId.ToString(), Course = ToCourse(courseCode), TeamId = teamId.ToString(), Limit = limit,
            }, options);
            return new SuggestedMembers(response.MissingSpecializationIds.ToList(), response.Suggestions.Select(s => new SuggestedMember(
                Guid.Parse(s.StudentId), s.SpecializationId, s.Score, s.Reasons.ToList(),
                s.SkillRatings.OrderByDescending(r => r.Level).Select(r => $"{SkillLabel(r.Skill)} {r.Level}/5").ToList())).ToList());
        }, ct);

    public Task<IReadOnlyList<SuggestedTeam>> SuggestTeamsAsync(Guid semesterId, string courseCode, Guid studentId, int limit, CancellationToken ct) =>
        CallAsync<IReadOnlyList<SuggestedTeam>>(async options =>
        {
            var response = await grpc.SuggestTeamsAsync(new SuggestTeamsRequest
            {
                SemesterId = semesterId.ToString(), Course = ToCourse(courseCode), StudentId = studentId.ToString(), Limit = limit,
            }, options);
            return response.Suggestions.Select(s => new SuggestedTeam(Guid.Parse(s.TeamId), s.Score, s.Reasons.ToList())).ToList();
        }, ct);

    /// <summary>Tên kỹ năng như Matching dùng trong lý do gợi ý (rule 16).</summary>
    private static string SkillLabel(Skill skill) => skill switch
    {
        Skill.Technology => "Công nghệ",
        Skill.UiUxDesign => "Thiết kế UI/UX",
        Skill.Management => "Quản lý",
        Skill.Marketing => "Marketing",
        Skill.CommunicationPlanning => "Truyền thông – kế hoạch",
        _ => skill.ToString(),
    };

    internal static Course ToCourse(string courseCode) => courseCode == "EXE201" ? Course.Exe201 : Course.Exe101;

    private static async Task<T> CallAsync<T>(Func<CallOptions, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(new CallOptions(deadline: DateTime.UtcNow.Add(Timeout), cancellationToken: ct));
        }
        catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled || !ct.IsCancellationRequested)
        {
            throw MatchingErrors.Unavailable();
        }
    }
}

public static class MatchingErrors
{
    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static HttpStatusException Unavailable() =>
        new(503, "matching-unavailable", "Chưa tính được gợi ý / kiểm tra nhóm", "Matching Service không phản hồi. Thử lại sau ít phút.");
}
