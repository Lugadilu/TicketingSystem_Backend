using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Api.Dtos.WhatsApp;
using TicketingSystem.Api.Services;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Support,TeamLead,SuperAdmin")]
[Route("api/whatsapp/support")]
public sealed class WhatsAppSupportController : ControllerBase
{
    private readonly IWhatsAppSenderService _sender;
    private readonly ILogger<WhatsAppSupportController> _logger;

    public WhatsAppSupportController(
        IWhatsAppSenderService sender,
        ILogger<WhatsAppSupportController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("reply")]
    public async Task<IActionResult> Reply(
        [FromBody] WhatsAppSupportReplyDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.ToPhoneNumber))
            return BadRequest("ToPhoneNumber is required.");

        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest("Message is required.");

        await _sender.SendTextMessageAsync(
            dto.ToPhoneNumber,
            dto.Message,
            cancellationToken);

        _logger.LogInformation(
            "Support WhatsApp reply sent to {ToPhoneNumber}.",
            dto.ToPhoneNumber);

        return Ok(new
        {
            message = "Reply sent successfully"
        });
    }
}