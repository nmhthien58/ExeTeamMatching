using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Messages;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Realtime;
using Collab.Services.Workspaces;

namespace Collab.Services.Messages;

public interface IMessageService
{
    /// <summary>C02 – kênh Nhóm (chỉ sinh viên của nhóm) và Cố vấn, kèm số chưa đọc và tin mới nhất.</summary>
    Task<IReadOnlyList<ChannelResponse>> ListChannelsAsync(Guid workspaceId, WorkspaceAccess viewer, CancellationToken ct);

    /// <summary>C03 – tin gốc mới nhất trước, phân trang cursor.</summary>
    Task<MessagePageResponse> ListMessagesAsync(Guid channelId, MessageQuery query, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// Gửi tin; <c>clientMessageId</c> làm id của tin để chống gửi trùng khi Thử lại (Created = false: tin đã có).
    /// Đẩy <c>messageCreated</c> qua hub; câu hỏi cho cố vấn, trả lời của cố vấn, @nhắc tên ghi sự kiện exe.collab (rule 22).
    /// </summary>
    Task<(MessageResponse Message, bool Created)> SendAsync(Guid channelId, SendMessageRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<ThreadResponse> GetThreadAsync(Guid messageId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Tác giả sửa tin trong 15 phút.</summary>
    Task<MessageResponse> EditAsync(Guid messageId, EditMessageRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Tác giả xóa mềm tin của mình.</summary>
    Task DeleteAsync(Guid messageId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Đã đọc tới tin nào (chỉ tiến, không lùi); đẩy <c>readUpdated</c>.</summary>
    Task MarkReadAsync(Guid channelId, MarkReadRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>C02 "Cần trả lời" – câu hỏi cho cố vấn của mọi nhóm mình là giảng viên hướng dẫn / mentor.</summary>
    Task<AdvisorQuestionPage> ListAdvisorQuestionsAsync(AdvisorQuestionQuery query, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Sự kiện hub có trong collab.yaml nhưng chưa có trong <see cref="HubEvents"/> (đề xuất chuyển vào đó).</summary>
public static class MessageHubEvents
{
    public const string MessageDeleted = "messageDeleted";
    public const string QuestionStatusChanged = "questionStatusChanged";
}

/// <summary>Module Messages của Collaboration (BE-69, rule 22).</summary>
public sealed class MessageService(
    IMessageRepository messages,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceAccessService access,
    IFileRepository files,
    IRealtimeNotifier realtime,
    TimeProvider clock) : IMessageService
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<ChannelResponse>> ListChannelsAsync(Guid workspaceId, WorkspaceAccess viewer, CancellationToken ct)
    {
        var channels = (await messages.ListChannelsAsync(workspaceId, ct)).Where(c => CanSee(c, viewer)).ToList();
        var ids = channels.Select(c => c.Id).ToList();
        var latest = await messages.LatestRootsAsync(ids, ct);
        var unread = viewer.IsReadOnly ? new Dictionary<Guid, int>() : await messages.CountUnreadAsync(ids, viewer.PersonType, viewer.PersonId, ct);
        var mapped = await MapAsync(latest.Values.ToList(), await PeopleAsync(workspaceId, ct), ct);
        return channels.Select(c => new ChannelResponse(c.Id, c.WorkspaceId, c.Kind, unread.GetValueOrDefault(c.Id),
            latest.TryGetValue(c.Id, out var last) ? mapped[last.Id] : null)).ToList();
    }

    public async Task<MessagePageResponse> ListMessagesAsync(Guid channelId, MessageQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var (channel, _) = await AccessAsync(channelId, user, write: false, ct);
        var before = query.Before is null ? null : await messages.GetAsync(Guid.Parse(query.Before), ct);
        if (before is not null && before.ChannelId != channelId)
        {
            before = null;
        }
        var rows = await messages.ListRootsAsync(channelId, before, query.Limit, ct);
        var mapped = await MapAsync(rows, await PeopleAsync(channel.WorkspaceId, ct), ct);
        return new MessagePageResponse(rows.Select(m => mapped[m.Id]).ToList(), rows.Count == query.Limit ? rows[^1].Id.ToString() : null);
    }

    public async Task<(MessageResponse Message, bool Created)> SendAsync(Guid channelId, SendMessageRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (channel, sender) = await AccessAsync(channelId, user, write: true, ct);
        var members = await lookup.ListMembersAsync(channel.WorkspaceId, ct);
        var people = new WorkspacePeople(members);
        var active = members.Where(m => m.LeftAt is null).ToList();

        if (await messages.GetAsync(request.ClientMessageId, ct) is { } existing)
        {
            if (existing.ChannelId != channelId || existing.AuthorPersonId != sender.PersonId)
            {
                throw MessageErrors.Invalid("clientMessageId", "clientMessageId đã được dùng cho tin khác.");
            }
            return ((await MapAsync([existing], people, ct))[existing.Id], false);
        }

        var parent = request.ParentMessageId is { } parentId ? await messages.GetAsync(parentId, ct) : null;
        if (request.ParentMessageId is not null
            && (parent is null || parent.ChannelId != channelId || parent.ParentMessageId is not null || parent.DeletedAt is not null))
        {
            throw MessageErrors.Invalid("parentMessageId", "Chỉ trả lời được tin gốc trong cùng kênh.");
        }
        if (request.IsAdvisorQuestion && (channel.Kind != ChannelKinds.Advisors || !sender.IsStudent))
        {
            throw MessageErrors.Invalid("isAdvisorQuestion", "Chỉ thành viên nhóm hỏi cố vấn, trong kênh Cố vấn.");
        }

        var attachmentIds = (request.AttachmentFileIds ?? []).Distinct().ToList();
        if (attachmentIds.Count > 0)
        {
            var found = await files.ListWithLatestAsync(attachmentIds, ct);
            if (found.Count != attachmentIds.Count || found.Any(f => f.File.WorkspaceId != channel.WorkspaceId || f.File.DeletedAt is not null))
            {
                throw MessageErrors.Invalid("attachmentFileIds", "Chỉ đính kèm được file của dự án này.");
            }
        }

        var workspace = (await workspaces.GetByIdAsync(channel.WorkspaceId, ct))!;
        var now = clock.GetUtcNow();
        var message = new Message
        {
            Id = request.ClientMessageId, ChannelId = channelId, ParentMessageId = parent?.Id, AuthorPersonType = sender.PersonType,
            AuthorPersonId = sender.PersonId, AuthorName = people.NameOf(sender.PersonType, sender.PersonId), Body = request.Body.Trim(),
            IsAdvisorQuestion = request.IsAdvisorQuestion, CreatedAt = now,
        };
        messages.Add(message);
        foreach (var fileId in attachmentIds)
        {
            messages.Add(new MessageAttachment { MessageId = message.Id, FileId = fileId });
        }

        var senderKey = new PersonKey(sender.PersonType, sender.PersonId);
        if (request.IsAdvisorQuestion)
        {
            messages.Add(new AdvisorQuestion { MessageId = message.Id, WorkspaceId = workspace.Id, Status = QuestionStatuses.Open });
            var advisors = active.Where(m => m.Role is WorkspaceRoles.Instructor or WorkspaceRoles.Mentor)
                .Select(m => new PersonKey(m.PersonType, m.PersonId)).ToList();
            messages.AddEvent(MessageEventTypes.AdvisorQuestionAsked, workspace.Id,
                new AdvisorQuestionAsked(workspace.Id, workspace.TeamId, message.Id, senderKey, Preview(message.Body), advisors));
        }

        // Cố vấn trả lời đầu tiên trong luồng của câu hỏi đang mở → ANSWERED.
        var answered = parent is not null && sender.IsAdvisor ? await messages.GetQuestionAsync(parent.Id, ct) : null;
        if (answered is { Status: QuestionStatuses.Open })
        {
            answered.Status = QuestionStatuses.Answered;
            (answered.AnsweredByPersonType, answered.AnsweredByPersonId, answered.AnsweredAt) = (sender.PersonType, sender.PersonId, now);
            messages.AddEvent(MessageEventTypes.AdvisorQuestionAnswered, workspace.Id, new AdvisorQuestionAnswered(workspace.Id, workspace.TeamId,
                parent!.Id, senderKey, [new PersonKey(parent.AuthorPersonType, parent.AuthorPersonId)]));
        }
        else
        {
            answered = null;
        }

        var mentioned = active
            .Where(m => request.MentionPersonIds?.Contains(m.PersonId) == true && m.PersonId != sender.PersonId && CanSee(channel, m))
            .Select(m => new PersonKey(m.PersonType, m.PersonId)).ToList();
        if (mentioned.Count > 0)
        {
            messages.AddEvent(MessageEventTypes.UserMentioned, workspace.Id, new UserMentioned(workspace.Id, channel.Kind, message.Id, mentioned));
        }

        await messages.SaveChangesAsync(ct);
        var response = (await MapAsync([message], people, ct))[message.Id];
        await realtime.ToChannelAsync(channelId, HubEvents.MessageCreated, response, ct);
        if (answered is not null)
        {
            await realtime.ToChannelAsync(channelId, MessageHubEvents.QuestionStatusChanged,
                new { channelId, messageId = answered.MessageId, status = answered.Status }, ct);
        }
        return (response, true);
    }

    public async Task<ThreadResponse> GetThreadAsync(Guid messageId, ClaimsPrincipal user, CancellationToken ct)
    {
        var root = await messages.GetAsync(messageId, ct) ?? throw MessageErrors.NotFound(messageId);
        if (root.ParentMessageId is { } parentId)
        {
            root = await messages.GetAsync(parentId, ct) ?? throw MessageErrors.NotFound(messageId);
        }
        var (channel, _) = await AccessAsync(root.ChannelId, user, write: false, ct);
        var replies = await messages.ListRepliesAsync(root.Id, ct);
        var mapped = await MapAsync([root, .. replies], await PeopleAsync(channel.WorkspaceId, ct), ct);
        return new ThreadResponse(mapped[root.Id], replies.Select(r => mapped[r.Id]).ToList());
    }

    public async Task<MessageResponse> EditAsync(Guid messageId, EditMessageRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (message, channel) = await RequireAuthorAsync(messageId, user, ct);
        if (clock.GetUtcNow() - message.CreatedAt > EditWindow)
        {
            throw MessageErrors.EditWindowClosed();
        }
        (message.Body, message.EditedAt) = (request.Body.Trim(), clock.GetUtcNow());
        await messages.SaveChangesAsync(ct);
        var response = (await MapAsync([message], await PeopleAsync(channel.WorkspaceId, ct), ct))[message.Id];
        await realtime.ToChannelAsync(channel.Id, HubEvents.MessageUpdated, response, ct);
        return response;
    }

    public async Task DeleteAsync(Guid messageId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (message, channel) = await RequireAuthorAsync(messageId, user, ct);
        message.DeletedAt = clock.GetUtcNow();
        await messages.SaveChangesAsync(ct);
        await realtime.ToChannelAsync(channel.Id, MessageHubEvents.MessageDeleted, new { channelId = channel.Id, messageId }, ct);
    }

    public async Task MarkReadAsync(Guid channelId, MarkReadRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (_, reader) = await AccessAsync(channelId, user, write: true, ct);
        var message = await messages.GetAsync(request.LastReadMessageId, ct);
        if (message is null || message.ChannelId != channelId)
        {
            throw MessageErrors.Invalid("lastReadMessageId", "Tin không thuộc kênh này.");
        }

        var read = await messages.GetReadAsync(channelId, reader.PersonType, reader.PersonId, ct);
        if (read is null)
        {
            messages.Add(new MessageRead
            {
                ChannelId = channelId, PersonType = reader.PersonType, PersonId = reader.PersonId, LastReadMessageId = message.Id, LastReadAt = message.CreatedAt,
            });
        }
        else if (message.CreatedAt > read.LastReadAt)
        {
            (read.LastReadMessageId, read.LastReadAt) = (message.Id, message.CreatedAt);
        }
        else
        {
            return; // không lùi mốc đã đọc
        }
        await messages.SaveChangesAsync(ct);
        await realtime.ToChannelAsync(channelId, HubEvents.ReadUpdated,
            new { channelId, personType = reader.PersonType, personId = reader.PersonId, lastReadMessageId = message.Id }, ct);
    }

    public async Task<AdvisorQuestionPage> ListAdvisorQuestionsAsync(AdvisorQuestionQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var advising = (await workspaces.ListForPeopleAsync(access.PeopleOf(user), ct))
            .Where(w => w.Role is WorkspaceRoles.Instructor or WorkspaceRoles.Mentor)
            .Select(w => w.Workspace.Id).Distinct().ToList();
        if (advising.Count == 0)
        {
            return new AdvisorQuestionPage([], query.Page, query.PageSize, 0, 0);
        }

        var (rows, total) = await messages.ListQuestionsAsync(advising, query.Status, query.Page, query.PageSize, ct);
        var members = await lookup.ListMembersAsync(rows.Select(r => r.Workspace.Id).Distinct().ToList(), ct);
        var now = clock.GetUtcNow();
        var items = new List<AdvisorQuestionResponse>();
        foreach (var row in rows)
        {
            var message = (await MapAsync([row.Message], new WorkspacePeople(members[row.Workspace.Id]), ct))[row.Message.Id];
            var waited = (row.Question.AnsweredAt ?? now) - row.Message.CreatedAt;
            items.Add(new AdvisorQuestionResponse(message, row.Workspace.Id, row.Workspace.TeamName, row.Question.Status, (int)waited.TotalHours));
        }
        return new AdvisorQuestionPage(items, query.Page, query.PageSize, total, (total + query.PageSize - 1) / query.PageSize);
    }

    /// <summary>Kênh Nhóm chỉ cho sinh viên của nhóm; kênh Cố vấn cho mọi người của workspace (rule 22).</summary>
    private static bool CanSee(Channel channel, WorkspaceAccess viewer) => channel.Kind != ChannelKinds.Team || viewer.IsStudent;

    private static bool CanSee(Channel channel, WorkspaceMember member) =>
        channel.Kind != ChannelKinds.Team || member.Role is WorkspaceRoles.Leader or WorkspaceRoles.Member;

    private async Task<(Channel Channel, WorkspaceAccess Access)> AccessAsync(Guid channelId, ClaimsPrincipal user, bool write, CancellationToken ct)
    {
        var channel = await workspaces.GetChannelAsync(channelId, ct) ?? throw new NotFoundException("kênh", channelId);
        var viewer = await access.RequireAsync(channel.WorkspaceId, user, ct);
        if (!CanSee(channel, viewer))
        {
            throw MessageErrors.TeamChannelOnly();
        }
        if (write)
        {
            WorkspaceMemberHandler.Check(viewer, WorkspaceAccessLevel.Writer);
        }
        return (channel, viewer);
    }

    private async Task<(Message Message, Channel Channel)> RequireAuthorAsync(Guid messageId, ClaimsPrincipal user, CancellationToken ct)
    {
        var message = await messages.GetAsync(messageId, ct) ?? throw MessageErrors.NotFound(messageId);
        var (channel, author) = await AccessAsync(message.ChannelId, user, write: true, ct);
        if (message.AuthorPersonType != author.PersonType || message.AuthorPersonId != author.PersonId)
        {
            throw MessageErrors.NotAuthor();
        }
        return message.DeletedAt is null ? (message, channel) : throw MessageErrors.NotFound(messageId);
    }

    private async Task<WorkspacePeople> PeopleAsync(Guid workspaceId, CancellationToken ct) => new(await lookup.ListMembersAsync(workspaceId, ct));

    private async Task<Dictionary<Guid, MessageResponse>> MapAsync(IReadOnlyList<Message> rows, WorkspacePeople people, CancellationToken ct)
    {
        var ids = rows.Select(m => m.Id).ToList();
        var replies = await messages.CountRepliesAsync(ids, ct);
        var questions = await messages.ListQuestionsAsync(ids, ct);
        var attachments = await messages.ListAttachmentIdsAsync(ids, ct);
        var fileIds = attachments.SelectMany(a => a).Distinct().ToList();
        var attached = (fileIds.Count == 0 ? [] : await files.ListWithLatestAsync(fileIds, ct))
            .Where(f => f.File.DeletedAt is null).ToDictionary(f => f.File.Id); // file đã xóa ở C06 thì thôi không hiện
        return rows.ToDictionary(m => m.Id, m => new MessageResponse(
            m.Id, m.Id, m.ChannelId, m.ParentMessageId, people.Ref(m.AuthorPersonType, m.AuthorPersonId, m.AuthorName),
            m.DeletedAt is null ? m.Body : string.Empty, m.DeletedAt is not null, m.IsAdvisorQuestion, questions.GetValueOrDefault(m.Id)?.Status,
            replies.GetValueOrDefault(m.Id),
            m.DeletedAt is null ? [.. attachments[m.Id].Where(attached.ContainsKey).Select(id => FileService.ToResponse(attached[id], isArchive: false))] : [],
            m.CreatedAt, m.EditedAt));
    }

    private static string Preview(string body) => body.Length <= 120 ? body : body[..120];
}
