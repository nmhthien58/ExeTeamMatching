using Collab.Services.Messages;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Messages;

/// <summary>Tin nhắn nhóm và cố vấn C02, C03 (BE-69, rule 22) – collab.yaml tag Messages.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class MessagesController(IMessageService messages) : ControllerBase
{
    /// <summary>Kênh Nhóm (chỉ thành viên nhóm) và Cố vấn, kèm số chưa đọc.</summary>
    [HttpGet("workspaces/{workspaceId:guid}/channels")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<IReadOnlyList<ChannelResponse>>> Channels(Guid workspaceId, CancellationToken ct) =>
        Ok(await messages.ListChannelsAsync(workspaceId, HttpContext.GetWorkspaceAccess(), ct));

    /// <summary>Tin gốc mới nhất trước, phân trang cursor (<c>before</c> = nextCursor của trang trước).</summary>
    [HttpGet("channels/{channelId:guid}/messages")]
    public async Task<ActionResult<MessagePageResponse>> List(Guid channelId, [FromQuery] MessageQuery query, CancellationToken ct) =>
        Ok(await messages.ListMessagesAsync(channelId, query, User, ct));

    /// <summary>201 khi tạo mới, 200 khi trùng <c>clientMessageId</c> (Thử lại).</summary>
    [HttpPost("channels/{channelId:guid}/messages")]
    public async Task<ActionResult<MessageResponse>> Send(Guid channelId, SendMessageRequest request, CancellationToken ct)
    {
        var (message, created) = await messages.SendAsync(channelId, request, User, ct);
        return created ? StatusCode(StatusCodes.Status201Created, message) : Ok(message);
    }

    [HttpGet("messages/{messageId:guid}/thread")]
    public async Task<ActionResult<ThreadResponse>> Thread(Guid messageId, CancellationToken ct) =>
        Ok(await messages.GetThreadAsync(messageId, User, ct));

    [HttpPatch("messages/{messageId:guid}")]
    public async Task<ActionResult<MessageResponse>> Edit(Guid messageId, EditMessageRequest request, CancellationToken ct) =>
        Ok(await messages.EditAsync(messageId, request, User, ct));

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken ct)
    {
        await messages.DeleteAsync(messageId, User, ct);
        return NoContent();
    }

    [HttpPost("channels/{channelId:guid}/read")]
    public async Task<IActionResult> Read(Guid channelId, MarkReadRequest request, CancellationToken ct)
    {
        await messages.MarkReadAsync(channelId, request, User, ct);
        return NoContent();
    }

    /// <summary>C02 "Cần trả lời" – câu hỏi cho cố vấn của mọi nhóm mình phụ trách.</summary>
    [HttpGet("advisor/questions")]
    public async Task<ActionResult<AdvisorQuestionPage>> Questions([FromQuery] AdvisorQuestionQuery query, CancellationToken ct) =>
        Ok(await messages.ListAdvisorQuestionsAsync(query, User, ct));
}
