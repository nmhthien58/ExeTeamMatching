using Collab.Services.Files;
using FluentValidation;

namespace Collab.Services.Messages;

/// <summary>Schema <c>Message</c> (C03). <c>attachments</c> chưa hỗ trợ nên luôn rỗng.</summary>
public sealed record MessageResponse(
    Guid Id, Guid? ClientMessageId, Guid ChannelId, Guid? ParentMessageId, PersonRef Author, string Body, bool IsDeleted,
    bool IsAdvisorQuestion, string? QuestionStatus, int ReplyCount, IReadOnlyList<ProjectFileResponse> Attachments,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

/// <summary>Schema <c>Channel</c> (C02).</summary>
public sealed record ChannelResponse(Guid Id, Guid WorkspaceId, string Kind, int UnreadCount, MessageResponse? LastMessage);

public sealed record MessagePageResponse(IReadOnlyList<MessageResponse> Items, string? NextCursor);

public sealed record ThreadResponse(MessageResponse Root, IReadOnlyList<MessageResponse> Replies);

/// <summary>Schema <c>AdvisorQuestion</c> (C02 "Cần trả lời").</summary>
public sealed record AdvisorQuestionResponse(MessageResponse Message, Guid WorkspaceId, string TeamName, string Status, int WaitingHours);

public sealed record AdvisorQuestionPage(IReadOnlyList<AdvisorQuestionResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record SendMessageRequest(
    Guid ClientMessageId, string Body, Guid? ParentMessageId, bool IsAdvisorQuestion, IReadOnlyList<Guid>? AttachmentFileIds,
    IReadOnlyList<Guid>? MentionPersonIds);

public sealed record EditMessageRequest(string Body);

public sealed record MarkReadRequest(Guid LastReadMessageId);

public sealed record MessageQuery
{
    public string? Before { get; init; }
    public int Limit { get; init; } = 30;
}

public sealed record AdvisorQuestionQuery
{
    public string Status { get; init; } = "OPEN";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.ClientMessageId).NotEmpty();
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Nhập nội dung tin nhắn.")
            .MaximumLength(2000).WithMessage("Tin nhắn tối đa 2.000 ký tự.");
        // shortcut: đính kèm file chưa làm (không nằm trong BE-69); bổ sung khi màn C03 cần, dùng lại mapper của module Files.
        RuleFor(x => x.AttachmentFileIds).Must(ids => ids is null || ids.Count == 0).WithMessage("Chưa hỗ trợ đính kèm file trong tin nhắn.");
        RuleFor(x => x.IsAdvisorQuestion).Equal(false).When(x => x.ParentMessageId is not null)
            .WithMessage("Câu hỏi cho cố vấn phải là tin gốc, không phải trả lời trong luồng.");
    }
}

public sealed class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator() =>
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Nhập nội dung tin nhắn.")
            .MaximumLength(2000).WithMessage("Tin nhắn tối đa 2.000 ký tự.");
}

public sealed class MessageQueryValidator : AbstractValidator<MessageQuery>
{
    public MessageQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        RuleFor(x => x.Before).Must(b => b is null || Guid.TryParse(b, out _)).WithMessage("Cursor không hợp lệ.");
    }
}

public sealed class AdvisorQuestionQueryValidator : AbstractValidator<AdvisorQuestionQuery>
{
    public AdvisorQuestionQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is "OPEN" or "ANSWERED").WithMessage("status phải là OPEN hoặc ANSWERED.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// Payload sự kiện exe.collab của module Messages (docs/contracts/events.md), key = workspaceId.
// Tin nhắn thường không có sự kiện Kafka – chỉ đẩy realtime qua hub.

public static class MessageEventTypes
{
    public const string AdvisorQuestionAsked = "AdvisorQuestionAsked";
    public const string AdvisorQuestionAnswered = "AdvisorQuestionAnswered";
    public const string UserMentioned = "UserMentioned";
}

public sealed record PersonKey(string PersonType, Guid PersonId);

public sealed record AdvisorQuestionAsked(Guid WorkspaceId, Guid TeamId, Guid MessageId, PersonKey AskedBy, string Preview, IReadOnlyList<PersonKey> Recipients);

public sealed record AdvisorQuestionAnswered(Guid WorkspaceId, Guid TeamId, Guid MessageId, PersonKey AnsweredBy, IReadOnlyList<PersonKey> Recipients);

public sealed record UserMentioned(Guid WorkspaceId, string ChannelKind, Guid MessageId, IReadOnlyList<PersonKey> Mentioned);
