using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.Invitations;

public static class RequestStatuses
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Pending, Accepted, Declined, Expired, Cancelled];

    /// <summary>Trạng thái hiển thị: PENDING đã quá hạn (job chưa chạy) trả EXPIRED.</summary>
    public static string Display(string status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == Pending && expiresAt <= now ? Expired : status;
}

/// <summary>Schema <c>Invitation</c> (S04, S12, S18–S20).</summary>
public sealed record InvitationResponse(
    Guid Id, TeamCardResponse? Team, StudentRef Invitee, string? Message, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record SendInvitationRequest(Guid StudentId, string? Message);

/// <summary>Kết quả <c>POST /internal/invitations/expire-overdue</c>.</summary>
public sealed record ExpireResult(int Invitations, int JoinRequests);

public sealed record RequestStatusQuery(string? Status);

public sealed class SendInvitationRequestValidator : AbstractValidator<SendInvitationRequest>
{
    public SendInvitationRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(500).WithMessage("Lời nhắn tối đa 500 ký tự.");
    }
}

public sealed class RequestStatusQueryValidator : AbstractValidator<RequestStatusQuery>
{
    public RequestStatusQueryValidator() =>
        RuleFor(x => x.Status).Must(s => s is null || RequestStatuses.All.Contains(s))
            .WithMessage("status phải là PENDING, ACCEPTED, DECLINED, EXPIRED hoặc CANCELLED.");
}

// Payload exe.invitation (docs/contracts/events.md).

public static class InvitationEventTypes
{
    public const string InvitationSent = "InvitationSent";
    public const string InvitationAccepted = "InvitationAccepted";
    public const string InvitationDeclined = "InvitationDeclined";
    public const string InvitationExpired = "InvitationExpired";
}

public sealed record InvitationSent(Guid InvitationId, Guid TeamId, string TeamName, Guid InviteeStudentId, Guid InvitedByStudentId, DateTimeOffset ExpiresAt);

/// <summary>Dùng cho InvitationAccepted / InvitationDeclined / InvitationExpired.</summary>
public sealed record InvitationResolved(Guid InvitationId, Guid TeamId, Guid InviteeStudentId, Guid LeaderStudentId);
