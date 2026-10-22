using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Core;
using static Matching.Grpc.Services.ProtoMapping;
// Tên kiểu của Matching.Services trùng message proto (MemberSuggestion, NewTeamPlan…) nên chỉ đặt alias cho kiểu cần dùng.
using AutoAssignmentPlanner = Matching.Services.AutoAssignment.AutoAssignmentPlanner;
using CompositionMember = Matching.Services.Composition.CompositionMember;
using CompositionRules = Matching.Services.Composition.CompositionRules;
using CompositionValidator = Matching.Services.Composition.CompositionValidator;
using SuggestionService = Matching.Services.Suggestions.SuggestionService;
using TopicQuery = Matching.Services.TopicSuggestions.TopicQuery;
using TopicSuggestionService = Matching.Services.TopicSuggestions.TopicSuggestionService;

namespace Matching.Grpc.Services;

/// <summary>
/// Matching gRPC (BE-34): chỉ đổi message proto ↔ model rồi gọi Matching.Services; lỗi nghiệp vụ được
/// <c>GrpcServerInterceptor</c> đổi sang status code. PlanAdvisorMatching thuộc module AdvisorMatching (BE-79) –
/// chưa override nên trả <c>Unimplemented</c>.
/// </summary>
public class MatchingGrpcService(
    SuggestionService suggestions,
    TopicSuggestionService topics,
    AutoAssignmentPlanner autoAssignment) : MatchingService.MatchingServiceBase
{
    public override async Task<SuggestMembersResponse> SuggestMembers(SuggestMembersRequest request, ServerCallContext context)
    {
        var result = await suggestions.SuggestMembersAsync(ParseId(request.TeamId, "teamId"), request.Limit, context.CancellationToken);

        var response = new SuggestMembersResponse();
        response.MissingSpecializationIds.AddRange(result.MissingSpecializationIds);
        response.WeakSkills.AddRange(result.WeakSkills.Select(ToProto));
        response.Suggestions.AddRange(result.Suggestions.Select(s =>
        {
            var suggestion = new MemberSuggestion
            {
                StudentId = s.Student.Id.ToString(),
                SpecializationId = s.Student.SpecializationId,
                Score = s.Score,
            };
            suggestion.Reasons.AddRange(s.Reasons);
            suggestion.SkillRatings.AddRange(ToProtoRatings(s.Student));
            return suggestion;
        }));
        return response;
    }

    public override async Task<SuggestTeamsResponse> SuggestTeams(SuggestTeamsRequest request, ServerCallContext context)
    {
        var result = await suggestions.SuggestTeamsAsync(ParseId(request.StudentId, "studentId"), request.Limit, context.CancellationToken);

        var response = new SuggestTeamsResponse();
        response.Suggestions.AddRange(result.Select(s =>
        {
            var suggestion = new TeamSuggestion
            {
                TeamId = s.Team.Id.ToString(),
                MemberCount = s.Team.MemberIds.Count,
                Score = s.Score,
            };
            suggestion.MissingSpecializationIds.AddRange(s.MissingSpecializationIds);
            suggestion.Reasons.AddRange(s.Reasons);
            return suggestion;
        }));
        return response;
    }

    public override async Task<SuggestTopicsForLecturerResponse> SuggestTopicsForLecturer(SuggestTopicsForLecturerRequest request, ServerCallContext context)
    {
        var page = await topics.SuggestAsync(new TopicQuery(
            ParseId(request.SemesterId, "semesterId"),
            ToCourseCode(request.Course),
            ToAdvisorKind(request.AdvisorKind),
            ParseId(request.LecturerId, "lecturerId"),
            request.Limit,
            request.Offset), context.CancellationToken);

        var response = new SuggestTopicsForLecturerResponse { Total = page.Total };
        response.Suggestions.AddRange(page.Items.Select(s =>
        {
            var suggestion = new TopicSuggestion
            {
                TeamId = s.Team.Id.ToString(),
                ProjectId = s.Team.ProjectId?.ToString() ?? string.Empty,
                InterestedLecturerCount = s.InterestedOthersCount,
                WasLecturerExe101Team = s.WasAdvisorExe101Team,
                Score = s.Score,
            };
            suggestion.MatchedDomainIds.AddRange(s.MatchedDomainIds);
            suggestion.Reasons.AddRange(s.Reasons);
            return suggestion;
        }));
        return response;
    }

    public override Task<ValidateCompositionResponse> ValidateComposition(ValidateCompositionRequest request, ServerCallContext context)
    {
        var rules = CompositionRules.From(request.MinSize, request.MaxSize, request.MinSpecializations);
        var members = request.Members.Select(m => new CompositionMember(ParseId(m.StudentId, "members.studentId"), m.SpecializationId));
        var result = CompositionValidator.Validate(members, rules);

        var response = new ValidateCompositionResponse
        {
            IsValid = result.IsValid,
            MemberCount = result.MemberCount,
            SpecializationCount = result.SpecializationCount,
        };
        response.Violations.AddRange(result.Violations.Select(v => new CompositionViolation { Code = v.Code, Message = v.Message }));
        return Task.FromResult(response);
    }

    public override async Task<PlanAutoAssignmentResponse> PlanAutoAssignment(PlanAutoAssignmentRequest request, ServerCallContext context)
    {
        var plan = await autoAssignment.PlanAsync(
            ParseId(request.SemesterId, "semesterId"),
            RequireCourseCode(request.Course),
            CompositionRules.From(request.MinSize, request.MaxSize, request.MinSpecializations),
            context.CancellationToken);

        var response = new PlanAutoAssignmentResponse();
        response.Fills.AddRange(plan.Fills.Select(f =>
        {
            var fill = new FillExistingTeam { TeamId = f.TeamId.ToString() };
            fill.StudentIds.AddRange(f.StudentIds.Select(id => id.ToString()));
            return fill;
        }));
        response.NewTeams.AddRange(plan.NewTeams.Select(t =>
        {
            var team = new NewTeamPlan
            {
                SuggestedName = t.SuggestedName,
                LeaderStudentId = t.LeaderStudentId.ToString(),
            };
            team.Members.AddRange(t.Members.Select(m => new MemberInfo { StudentId = m.StudentId.ToString(), SpecializationId = m.SpecializationId }));
            return team;
        }));
        response.UnassignedStudentIds.AddRange(plan.UnassignedStudentIds.Select(id => id.ToString()));
        return response;
    }
}
