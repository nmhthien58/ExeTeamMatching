using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Messages;

public static class ChannelKinds
{
    public const string Team = "TEAM";
    public const string Advisors = "ADVISORS";
}

public static class QuestionStatuses
{
    public const string Open = "OPEN";
    public const string Answered = "ANSWERED";
}

/// <summary>Câu hỏi cho cố vấn kèm tin gốc và workspace (C02 "Cần trả lời").</summary>
public sealed record QuestionRow(AdvisorQuestion Question, Message Message, Workspace Workspace);

public interface IMessageRepository
{
    Task<List<Channel>> ListChannelsAsync(Guid workspaceId, CancellationToken ct);

    /// <summary>Được theo dõi.</summary>
    Task<Message?> GetAsync(Guid messageId, CancellationToken ct);

    /// <summary>Tin gốc mới nhất trước, cũ hơn tin <paramref name="before"/> (cursor) nếu có.</summary>
    Task<List<Message>> ListRootsAsync(Guid channelId, Message? before, int limit, CancellationToken ct);

    Task<List<Message>> ListRepliesAsync(Guid rootMessageId, CancellationToken ct);

    Task<Dictionary<Guid, int>> CountRepliesAsync(IReadOnlyCollection<Guid> rootIds, CancellationToken ct);

    /// <summary>Id file đính kèm của từng tin.</summary>
    Task<ILookup<Guid, Guid>> ListAttachmentIdsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct);

    Task<Dictionary<Guid, AdvisorQuestion>> ListQuestionsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct);

    /// <summary>Được theo dõi.</summary>
    Task<AdvisorQuestion?> GetQuestionAsync(Guid messageId, CancellationToken ct);

    /// <summary>Tin gốc mới nhất của từng kênh.</summary>
    Task<Dictionary<Guid, Message>> LatestRootsAsync(IReadOnlyCollection<Guid> channelIds, CancellationToken ct);

    /// <summary>Số tin (cả trả lời, trừ tin của chính mình và tin đã xóa) sau lần đọc cuối của người đó, theo kênh.</summary>
    Task<Dictionary<Guid, int>> CountUnreadAsync(IReadOnlyCollection<Guid> channelIds, string personType, Guid personId, CancellationToken ct);

    /// <summary>Được theo dõi.</summary>
    Task<MessageRead?> GetReadAsync(Guid channelId, string personType, Guid personId, CancellationToken ct);

    /// <summary>Câu hỏi cho cố vấn của các workspace, cũ nhất trước (chờ lâu nhất lên đầu).</summary>
    Task<(List<QuestionRow> Items, int TotalCount)> ListQuestionsAsync(IReadOnlyCollection<Guid> workspaceIds, string status, int page, int pageSize, CancellationToken ct);

    void Add(object entity);

    /// <summary>Ghi sự kiện exe.collab vào outbox (key = workspaceId), cùng transaction với <see cref="SaveChangesAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class MessageRepository(CollabDbContext db) : IMessageRepository
{
    public Task<List<Channel>> ListChannelsAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<Channel>().AsNoTracking().Where(c => c.WorkspaceId == workspaceId).OrderBy(c => c.Kind == ChannelKinds.Advisors).ToListAsync(ct);

    public Task<Message?> GetAsync(Guid messageId, CancellationToken ct) => db.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public Task<List<Message>> ListRootsAsync(Guid channelId, Message? before, int limit, CancellationToken ct)
    {
        var query = db.Set<Message>().AsNoTracking().Where(m => m.ChannelId == channelId && m.ParentMessageId == null);
        if (before is not null)
        {
            query = query.Where(m => m.CreatedAt < before.CreatedAt || (m.CreatedAt == before.CreatedAt && m.Id.CompareTo(before.Id) < 0));
        }
        return query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(limit).ToListAsync(ct);
    }

    public Task<List<Message>> ListRepliesAsync(Guid rootMessageId, CancellationToken ct) =>
        db.Set<Message>().AsNoTracking().Where(m => m.ParentMessageId == rootMessageId).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);

    public async Task<Dictionary<Guid, int>> CountRepliesAsync(IReadOnlyCollection<Guid> rootIds, CancellationToken ct)
    {
        var ids = rootIds.Select(id => (Guid?)id).ToList();
        var counts = await db.Set<Message>().AsNoTracking()
            .Where(m => ids.Contains(m.ParentMessageId) && m.DeletedAt == null)
            .GroupBy(m => m.ParentMessageId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return counts.ToDictionary(c => c.Key!.Value, c => c.Count);
    }

    public async Task<ILookup<Guid, Guid>> ListAttachmentIdsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct) =>
        (await db.Set<MessageAttachment>().AsNoTracking().Where(a => messageIds.Contains(a.MessageId)).ToListAsync(ct))
            .ToLookup(a => a.MessageId, a => a.FileId);

    public Task<Dictionary<Guid, AdvisorQuestion>> ListQuestionsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct) =>
        db.Set<AdvisorQuestion>().AsNoTracking().Where(q => messageIds.Contains(q.MessageId)).ToDictionaryAsync(q => q.MessageId, ct);

    public Task<AdvisorQuestion?> GetQuestionAsync(Guid messageId, CancellationToken ct) =>
        db.Set<AdvisorQuestion>().FirstOrDefaultAsync(q => q.MessageId == messageId, ct);

    public async Task<Dictionary<Guid, Message>> LatestRootsAsync(IReadOnlyCollection<Guid> channelIds, CancellationToken ct)
    {
        var latest = new Dictionary<Guid, Message>();
        foreach (var channelId in channelIds) // mỗi workspace chỉ 2 kênh
        {
            if (await db.Set<Message>().AsNoTracking().Where(m => m.ChannelId == channelId && m.ParentMessageId == null)
                    .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).FirstOrDefaultAsync(ct) is { } message)
            {
                latest[channelId] = message;
            }
        }
        return latest;
    }

    public async Task<Dictionary<Guid, int>> CountUnreadAsync(IReadOnlyCollection<Guid> channelIds, string personType, Guid personId, CancellationToken ct)
    {
        var reads = await db.Set<MessageRead>().AsNoTracking()
            .Where(r => channelIds.Contains(r.ChannelId) && r.PersonType == personType && r.PersonId == personId)
            .ToDictionaryAsync(r => r.ChannelId, r => r.LastReadAt, ct);
        var counts = new Dictionary<Guid, int>();
        foreach (var channelId in channelIds)
        {
            var query = db.Set<Message>().Where(m => m.ChannelId == channelId && m.DeletedAt == null
                && !(m.AuthorPersonType == personType && m.AuthorPersonId == personId));
            if (reads.TryGetValue(channelId, out var since))
            {
                query = query.Where(m => m.CreatedAt > since);
            }
            counts[channelId] = await query.CountAsync(ct);
        }
        return counts;
    }

    public Task<MessageRead?> GetReadAsync(Guid channelId, string personType, Guid personId, CancellationToken ct) =>
        db.Set<MessageRead>().FirstOrDefaultAsync(r => r.ChannelId == channelId && r.PersonType == personType && r.PersonId == personId, ct);

    public async Task<(List<QuestionRow> Items, int TotalCount)> ListQuestionsAsync(
        IReadOnlyCollection<Guid> workspaceIds, string status, int page, int pageSize, CancellationToken ct)
    {
        var query = from q in db.Set<AdvisorQuestion>().AsNoTracking()
                    join m in db.Set<Message>().AsNoTracking() on q.MessageId equals m.Id
                    join w in db.Set<Workspace>().AsNoTracking() on q.WorkspaceId equals w.Id
                    where workspaceIds.Contains(q.WorkspaceId) && q.Status == status && m.DeletedAt == null
                    select new { q, m, w };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.m.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (rows.Select(x => new QuestionRow(x.q, x.m, x.w)).ToList(), total);
    }

    public void Add(object entity) => db.Add(entity);

    public void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, eventType, "Workspace", workspaceId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
