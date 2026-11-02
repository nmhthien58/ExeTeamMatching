using BuildingBlocks.Observability;
using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Matching.Grpc.Services;
using Matching.Services.AdvisorMatching;
using Matching.Services.AutoAssignment;
using Matching.Services.Suggestions;
using Matching.Services.TopicSuggestions;

namespace Matching.Tests;

/// <summary>BE-34 – lớp gRPC: map proto ↔ model, lỗi → status code qua interceptor, RPC chưa làm trả Unimplemented.</summary>
public sealed class MatchingGrpcServiceTests
{
    private readonly Scenario _s = new();

    private MatchingGrpcService Service => new(
        new SuggestionService(_s.Model), new TopicSuggestionService(_s.Model), new AutoAssignmentPlanner(_s.Model), new AdvisorMatchingPlanner());

    [Fact]
    public async Task ValidateComposition_maps_violations()
    {
        var request = new ValidateCompositionRequest();
        request.Members.Add(new MemberInfo { StudentId = Guid.NewGuid().ToString(), SpecializationId = 1 });

        var response = await Service.ValidateComposition(request, new FakeCallContext());

        Assert.False(response.IsValid);
        Assert.Equal(["TOO_FEW_MEMBERS", "NOT_ENOUGH_SPECIALIZATIONS"], response.Violations.Select(v => v.Code));
    }

    [Fact]
    public async Task SuggestMembers_returns_reasons_and_skill_ratings()
    {
        var team = await _s.TeamAsync("A", await _s.StudentAsync(Scenario.SE));
        var designer = await _s.StudentAsync(Scenario.GD, skills: [(Matching.Services.ReadModel.Skill.UiUxDesign, 4)]);

        var response = await Service.SuggestMembers(new SuggestMembersRequest { TeamId = team.ToString() }, new FakeCallContext());

        var suggestion = Assert.Single(response.Suggestions);
        Assert.Equal(designer.ToString(), suggestion.StudentId);
        Assert.Equal(Skill.UiUxDesign, Assert.Single(suggestion.SkillRatings).Skill);
        Assert.Contains(Scenario.GD, response.MissingSpecializationIds);
    }

    [Fact]
    public async Task PlanAutoAssignment_requires_course()
    {
        var request = new PlanAutoAssignmentRequest { SemesterId = _s.SemesterId.ToString() };

        var error = await Assert.ThrowsAsync<RpcException>(() => CallThroughInterceptor(request, Service.PlanAutoAssignment));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task Unknown_team_maps_to_not_found_and_bad_id_to_invalid_argument()
    {
        var notFound = await Assert.ThrowsAsync<RpcException>(() =>
            CallThroughInterceptor(new SuggestMembersRequest { TeamId = Guid.NewGuid().ToString() }, Service.SuggestMembers));
        var badId = await Assert.ThrowsAsync<RpcException>(() =>
            CallThroughInterceptor(new SuggestMembersRequest { TeamId = "abc" }, Service.SuggestMembers));

        Assert.Equal(StatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(StatusCode.InvalidArgument, badId.StatusCode);
    }

    [Fact]
    public async Task PlanAdvisorMatching_maps_matches_reasons_and_unmatched_teams()
    {
        var full = Guid.NewGuid().ToString();
        var open = Guid.NewGuid().ToString();
        var matched = new MatchingTeam { TeamId = Guid.NewGuid().ToString(), PreferredAdvisorIds = { full, open } };
        var unmatched = new MatchingTeam { TeamId = Guid.NewGuid().ToString(), PreferredAdvisorIds = { full } };
        var request = new PlanAdvisorMatchingRequest
        {
            Course = Course.Exe101,
            Teams = { matched, unmatched },
            Advisors =
            {
                new MatchingAdvisor { AdvisorId = full, CurrentTeamCount = 2, HasMaxTeams = true, MaxTeams = 2 },
                new MatchingAdvisor { AdvisorId = open, HasMaxTeams = true, MaxTeams = 1 },
            },
        };

        var response = await CallThroughInterceptor(request, Service.PlanAdvisorMatching);

        Assert.Equal(1, response.MatchedCount);
        Assert.Equal(1, response.UnmatchedCount);
        var first = response.Matches.Single(m => m.TeamId == matched.TeamId);
        Assert.Equal(open, first.AdvisorId);
        Assert.Equal(MatchReason.Preference, first.Reason);
        Assert.Equal(2, first.PreferenceRank);
        var second = response.Matches.Single(m => m.TeamId == unmatched.TeamId);
        Assert.Equal(string.Empty, second.AdvisorId);
        Assert.Equal(MatchReason.Unspecified, second.Reason);
    }

    [Fact]
    public async Task PlanAdvisorMatching_treats_missing_maximum_as_no_limit_and_rejects_bad_ids()
    {
        var advisor = Guid.NewGuid().ToString();
        var request = new PlanAdvisorMatchingRequest
        {
            Teams = { new MatchingTeam { TeamId = Guid.NewGuid().ToString() } },
            Advisors = { new MatchingAdvisor { AdvisorId = advisor, CurrentTeamCount = 40 } },
        };

        Assert.Equal(advisor, (await CallThroughInterceptor(request, Service.PlanAdvisorMatching)).Matches.Single().AdvisorId);

        request.Teams[0].PreviousInstructorId = "abc";
        var error = await Assert.ThrowsAsync<RpcException>(() => CallThroughInterceptor(request, Service.PlanAdvisorMatching));
        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task Interceptor_keeps_incoming_correlation_id()
    {
        string? seen = null;
        var context = new FakeCallContext(new Metadata { { CorrelationContext.GrpcHeaderName, "abc123" } });

        await new GrpcServerInterceptor().UnaryServerHandler<ValidateCompositionRequest, ValidateCompositionResponse>(
            new ValidateCompositionRequest(), context, (_, _) =>
            {
                seen = CorrelationContext.Id;
                return Task.FromResult(new ValidateCompositionResponse());
            });

        Assert.Equal("abc123", seen);
    }

    private static Task<TResponse> CallThroughInterceptor<TRequest, TResponse>(
        TRequest request, Func<TRequest, ServerCallContext, Task<TResponse>> handler)
        where TRequest : class
        where TResponse : class =>
        new GrpcServerInterceptor().UnaryServerHandler(request, new FakeCallContext(), (r, c) => handler(r, c));

    private sealed class FakeCallContext(Metadata? headers = null) : ServerCallContext
    {
        protected override string MethodCore => "/exe.matching.v1.MatchingService/Test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore { get; } = headers ?? [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, []);

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
