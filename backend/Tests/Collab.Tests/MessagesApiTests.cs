using System.Net;
using System.Net.Http.Json;
using Collab.Repositories.Messages;
using Collab.Services.Messages;

namespace Collab.Tests;

/// <summary>BE-69 – kênh Nhóm / Cố vấn, tin nhắn, luồng, đã đọc, câu hỏi cho cố vấn (rule 22).</summary>
public sealed class MessagesApiTests : IClassFixture<CollabApiFactory>
{
    private readonly CollabApiFactory _factory;
    private readonly TestPerson _leader = TestPerson.Leader();
    private readonly TestPerson _member = TestPerson.Member();
    private readonly TestPerson _instructor = TestPerson.Instructor();
    private readonly TestPerson _mentor = TestPerson.Mentor();
    private Guid _workspace;
    private Guid _teamChannel;
    private Guid _advisorChannel;

    public MessagesApiTests(CollabApiFactory factory) => _factory = factory;

    private async Task SeedAsync()
    {
        _workspace = await _factory.SeedWorkspaceAsync("Nhóm Chat", "EXE201", null, _leader, _member, _instructor, _mentor);
        (_teamChannel, _advisorChannel) = (Guid.NewGuid(), Guid.NewGuid());
        await _factory.WithDbAsync(db =>
        {
            db.AddRange(
                new Channel { Id = _teamChannel, WorkspaceId = _workspace, Kind = ChannelKinds.Team, CreatedAt = DateTimeOffset.UtcNow },
                new Channel { Id = _advisorChannel, WorkspaceId = _workspace, Kind = ChannelKinds.Advisors, CreatedAt = DateTimeOffset.UtcNow });
            return Task.CompletedTask;
        });
    }

    private static object Body(string text, Guid? parent = null, bool question = false, Guid[]? mentions = null) =>
        new { clientMessageId = Guid.NewGuid(), body = text, parentMessageId = parent, isAdvisorQuestion = question, mentionPersonIds = mentions };

    private async Task<MessageResponse> SendAsync(TestPerson by, Guid channel, object body)
    {
        var response = await _factory.ClientFor(by).PostAsJsonAsync($"/api/channels/{channel}/messages", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<MessageResponse>(response);
    }

    [Fact]
    public async Task Team_channel_is_students_only_and_advisors_see_only_advisor_channel()
    {
        await SeedAsync();

        var studentChannels = await CollabApiFactory.ReadAsync<List<ChannelResponse>>(
            await _factory.ClientFor(_member).GetAsync($"/api/workspaces/{_workspace}/channels"));
        var mentorChannels = await CollabApiFactory.ReadAsync<List<ChannelResponse>>(
            await _factory.ClientFor(_mentor).GetAsync($"/api/workspaces/{_workspace}/channels"));
        var mentorReadsTeam = await _factory.ClientFor(_mentor).GetAsync($"/api/channels/{_teamChannel}/messages");
        var adminSends = await _factory.ClientFor(TestPerson.Admin()).PostAsJsonAsync($"/api/channels/{_advisorChannel}/messages", Body("Bộ môn"));

        Assert.Equal([ChannelKinds.Team, ChannelKinds.Advisors], studentChannels.Select(c => c.Kind));
        Assert.Equal(ChannelKinds.Advisors, Assert.Single(mentorChannels).Kind);
        Assert.Equal("team-channel-only", await CollabApiFactory.ErrorCodeAsync(mentorReadsTeam));
        Assert.Equal("workspace-read-only", await CollabApiFactory.ErrorCodeAsync(adminSends));
    }

    [Fact]
    public async Task Send_is_idempotent_by_client_id_and_history_pages_newest_first()
    {
        await SeedAsync();
        var client = _factory.ClientFor(_leader);
        var retry = Body("Chào cả nhóm");

        var first = await client.PostAsJsonAsync($"/api/channels/{_teamChannel}/messages", retry);
        var again = await client.PostAsJsonAsync($"/api/channels/{_teamChannel}/messages", retry);
        await SendAsync(_member, _teamChannel, Body("Tin 2"));
        await SendAsync(_member, _teamChannel, Body("Tin 3"));
        var page1 = await CollabApiFactory.ReadAsync<MessagePageResponse>(await client.GetAsync($"/api/channels/{_teamChannel}/messages?limit=2"));
        var page2 = await CollabApiFactory.ReadAsync<MessagePageResponse>(
            await client.GetAsync($"/api/channels/{_teamChannel}/messages?limit=2&before={page1.NextCursor}"));
        var blank = await client.PostAsJsonAsync($"/api/channels/{_teamChannel}/messages", Body("   "));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(["Tin 3", "Tin 2"], page1.Items.Select(m => m.Body));
        Assert.Equal("Chào cả nhóm", Assert.Single(page2.Items).Body);
        Assert.Equal("Trưởng nhóm", page2.Items[0].Author.DisplayName);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [Fact]
    public async Task Advisor_question_is_answered_by_advisor_reply_and_events_are_written()
    {
        await SeedAsync();

        var question = await SendAsync(_member, _advisorChannel, Body("Thầy xem giúp mô hình kinh doanh?", question: true));
        var inTeamChannel = await _factory.ClientFor(_member).PostAsJsonAsync($"/api/channels/{_teamChannel}/messages", Body("Hỏi?", question: true));
        var open = await CollabApiFactory.ReadAsync<AdvisorQuestionPage>(await _factory.ClientFor(_instructor).GetAsync("/api/advisor/questions"));
        await SendAsync(_mentor, _advisorChannel, Body("Mình góp ý thế này…", parent: question.Id));
        var thread = await CollabApiFactory.ReadAsync<ThreadResponse>(await _factory.ClientFor(_leader).GetAsync($"/api/messages/{question.Id}/thread"));
        var stillOpen = await CollabApiFactory.ReadAsync<AdvisorQuestionPage>(await _factory.ClientFor(_instructor).GetAsync("/api/advisor/questions"));
        var studentList = await CollabApiFactory.ReadAsync<AdvisorQuestionPage>(await _factory.ClientFor(_member).GetAsync("/api/advisor/questions"));

        Assert.Equal("OPEN", question.QuestionStatus);
        Assert.Equal(HttpStatusCode.BadRequest, inTeamChannel.StatusCode);
        Assert.Equal("Nhóm Chat", Assert.Single(open.Items).TeamName);
        Assert.Equal("ANSWERED", thread.Root.QuestionStatus);
        Assert.Equal(1, thread.Root.ReplyCount);
        Assert.Empty(stillOpen.Items);
        Assert.Equal(0, studentList.TotalCount);
        var asked = Assert.Single(await _factory.EventsAsync(MessageEventTypes.AdvisorQuestionAsked), e => e.Payload.Contains(question.Id.ToString()));
        Assert.Contains(_instructor.Id.ToString(), asked.Payload);
        Assert.Contains(_mentor.Id.ToString(), asked.Payload);
        Assert.Contains(await _factory.EventsAsync(MessageEventTypes.AdvisorQuestionAnswered), e => e.Payload.Contains(question.Id.ToString()));
    }

    [Fact]
    public async Task Mentions_only_reach_people_who_can_see_the_channel()
    {
        await SeedAsync();

        var message = await SendAsync(_leader, _teamChannel, Body("@cả nhà", mentions: [_member.Id, _mentor.Id, Guid.NewGuid()]));

        var mentioned = Assert.Single(await _factory.EventsAsync(MessageEventTypes.UserMentioned), e => e.Payload.Contains(message.Id.ToString()));
        Assert.Contains(_member.Id.ToString(), mentioned.Payload);
        Assert.DoesNotContain(_mentor.Id.ToString(), mentioned.Payload);
    }

    [Fact]
    public async Task Author_edits_and_deletes_and_read_marker_clears_unread()
    {
        await SeedAsync();
        var message = await SendAsync(_leader, _teamChannel, Body("Họp lúc 9h"));
        var member = _factory.ClientFor(_member);

        var unreadBefore = (await CollabApiFactory.ReadAsync<List<ChannelResponse>>(await member.GetAsync($"/api/workspaces/{_workspace}/channels")))
            .Single(c => c.Kind == ChannelKinds.Team);
        var read = await member.PostAsJsonAsync($"/api/channels/{_teamChannel}/read", new { lastReadMessageId = message.Id });
        var unreadAfter = (await CollabApiFactory.ReadAsync<List<ChannelResponse>>(await member.GetAsync($"/api/workspaces/{_workspace}/channels")))
            .Single(c => c.Kind == ChannelKinds.Team);
        var editByOther = await member.PatchAsJsonAsync($"/api/messages/{message.Id}", new { body = "Sửa trộm" });
        var edited = await CollabApiFactory.ReadAsync<MessageResponse>(
            await _factory.ClientFor(_leader).PatchAsJsonAsync($"/api/messages/{message.Id}", new { body = "Họp lúc 10h" }));
        var deleted = await _factory.ClientFor(_leader).DeleteAsync($"/api/messages/{message.Id}");
        var history = await CollabApiFactory.ReadAsync<MessagePageResponse>(await member.GetAsync($"/api/channels/{_teamChannel}/messages"));

        Assert.Equal(1, unreadBefore.UnreadCount);
        Assert.Equal("Họp lúc 9h", unreadBefore.LastMessage!.Body);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(0, unreadAfter.UnreadCount);
        Assert.Equal(HttpStatusCode.Forbidden, editByOther.StatusCode);
        Assert.Equal("Họp lúc 10h", edited.Body);
        Assert.NotNull(edited.EditedAt);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var gone = Assert.Single(history.Items);
        Assert.True(gone.IsDeleted);
        Assert.Equal(string.Empty, gone.Body);
    }
}
