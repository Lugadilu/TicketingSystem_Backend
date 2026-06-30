using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketingSystem.Api.Dtos.WhatsApp;
using TicketingSystem.Api.Services;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Route("api/whatsapp/webhook")]
public sealed class WhatsAppWebhookController : ControllerBase
{
    private readonly WhatsAppOptions _options;
    private readonly IWhatsAppWebhookSignatureValidator _signatureValidator;
    private readonly IWhatsAppMessageQueue _messageQueue;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppOptions> options,
        IWhatsAppWebhookSignatureValidator signatureValidator,
        IWhatsAppMessageQueue messageQueue,
        ILogger<WhatsAppWebhookController> logger)
    {
        _options = options.Value;
        _signatureValidator = signatureValidator;
        _messageQueue = messageQueue;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" &&
            verifyToken == _options.VerifyToken &&
            !string.IsNullOrWhiteSpace(challenge))
        {
            _logger.LogInformation("WhatsApp webhook verified successfully.");
            return Content(challenge, "text/plain");
        }

        _logger.LogWarning("WhatsApp webhook verification failed.");
        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        if (!_signatureValidator.IsValid(signatureHeader, rawBody))
        {
            _logger.LogWarning("Rejected WhatsApp webhook because signature validation failed.");
            return Unauthorized();
        }

        WhatsAppWebhookDto? payload;

        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookDto>(
                rawBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize WhatsApp webhook payload.");
            return Ok();
        }

        if (payload == null)
            return Ok();

        foreach (var entry in payload.Entry)
        {
            foreach (var change in entry.Changes)
            {
                var contacts = change.Value.Contacts;
                var messages = change.Value.Messages;

                if (messages == null || messages.Count == 0)
                    continue;

                foreach (var message in messages)
                {
                    var contact = contacts?.FirstOrDefault(c => c.WaId == message.From);
                    var customerName = contact?.Profile.Name ?? "Customer";

                    var text = ExtractIncomingText(message);

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var internalMessage = new WhatsAppIncomingMessage(
                        message.Id,
                        message.From,
                        customerName,
                        text,
                        message.Type,
                        DateTime.UtcNow);

                    _messageQueue.Enqueue(internalMessage);
                }
            }
        }

        return Ok();
    }

    private static string ExtractIncomingText(WhatsAppMessageDto message)
    {
        if (message.Text != null)
            return message.Text.Body;

        if (message.Interactive?.ButtonReply != null)
            return message.Interactive.ButtonReply.Id;

        if (message.Interactive?.ListReply != null)
            return message.Interactive.ListReply.Id;

        return string.Empty;
    }
}