using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TicketingSystem.Api.Dtos.WhatsApp;
using TicketingSystem.Api.Hubs;
using TicketingSystem.Api.Services;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Support,TeamLead,SuperAdmin")]
[Route("api/whatsapp/support")]
public sealed class WhatsAppSupportController : ControllerBase
{
    private readonly IWhatsAppSenderService _sender;
    private readonly IWhatsAppConversationStore _conversationStore;
    private readonly IHubContext<WhatsAppLiveChatHub> _liveChatHub;
    private readonly ILogger<WhatsAppSupportController> _logger;

    public WhatsAppSupportController(
        IWhatsAppSenderService sender,
        IWhatsAppConversationStore conversationStore,
        IHubContext<WhatsAppLiveChatHub> liveChatHub,
        ILogger<WhatsAppSupportController> logger)
    {
        _sender = sender;
        _conversationStore = conversationStore;
        _liveChatHub = liveChatHub;
        _logger = logger;
    }

    [HttpPost("reply")]
    public async Task<IActionResult> Reply(
        [FromBody] WhatsAppSupportReplyDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.ConversationId == Guid.Empty)
            return BadRequest("ConversationId is required.");

        if (string.IsNullOrWhiteSpace(dto.ToPhoneNumber))
            return BadRequest("ToPhoneNumber is required.");

        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest("Message is required.");

        var senderUserId = GetCurrentUserId();

        var savedMessage = await _conversationStore.AddOutboundMessageAsync(
            dto.ConversationId,
            dto.Message,
            senderUserId,
            cancellationToken);

        await _sender.SendTextMessageAsync(
            dto.ToPhoneNumber,
            dto.Message,
            cancellationToken);

        var liveChatMessage = new WhatsAppLiveChatMessage(
            dto.ConversationId,
            savedMessage.Id,
            dto.ToPhoneNumber,
            "Support",
            dto.Message,
            "Outbound",
            savedMessage.CreatedAt);

        var conversationUpdated = new WhatsAppConversationUpdated(
            dto.ConversationId,
            dto.ToPhoneNumber,
            "Support",
            dto.Message,
            "Outbound",
            savedMessage.CreatedAt);

        await _liveChatHub.Clients.All.SendAsync(
            "ReceiveConversationUpdated",
            conversationUpdated,
            cancellationToken);

        await _liveChatHub.Clients
            .Group(WhatsAppLiveChatHub.GetConversationGroupName(dto.ConversationId))
            .SendAsync(
                "ReceiveWhatsAppMessage",
                liveChatMessage,
                cancellationToken);

        _logger.LogInformation(
            "Support WhatsApp reply sent to {ToPhoneNumber} for conversation {ConversationId}.",
            dto.ToPhoneNumber,
            dto.ConversationId);

        return Ok(new
        {
            message = "Reply sent successfully",
            conversationId = dto.ConversationId,
            messageId = savedMessage.Id
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId");

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}




// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using TicketingSystem.Api.Dtos.WhatsApp;
// using TicketingSystem.Api.Services;

// namespace TicketingSystem.Api.Controllers;

// [ApiController]
// [Authorize(Roles = "Support,TeamLead,SuperAdmin")]
// [Route("api/whatsapp/support")]
// public sealed class WhatsAppSupportController : ControllerBase
// {
//     private readonly IWhatsAppSenderService _sender;
//     private readonly ILogger<WhatsAppSupportController> _logger;

//     public WhatsAppSupportController(
//         IWhatsAppSenderService sender,
//         ILogger<WhatsAppSupportController> logger)
//     {
//         _sender = sender;
//         _logger = logger;
//     }

//     [HttpPost("reply")]
//     public async Task<IActionResult> Reply(
//         [FromBody] WhatsAppSupportReplyDto dto,
//         CancellationToken cancellationToken)
//     {
//         if (string.IsNullOrWhiteSpace(dto.ToPhoneNumber))
//             return BadRequest("ToPhoneNumber is required.");

//         if (string.IsNullOrWhiteSpace(dto.Message))
//             return BadRequest("Message is required.");

//         await _sender.SendTextMessageAsync(
//             dto.ToPhoneNumber,
//             dto.Message,
//             cancellationToken);

//         _logger.LogInformation(
//             "Support WhatsApp reply sent to {ToPhoneNumber}.",
//             dto.ToPhoneNumber);

//         return Ok(new
//         {
//             message = "Reply sent successfully"
//         });
//     }
// }