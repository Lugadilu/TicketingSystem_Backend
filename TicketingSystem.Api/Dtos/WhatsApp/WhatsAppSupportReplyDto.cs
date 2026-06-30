namespace TicketingSystem.Api.Dtos.WhatsApp;

public sealed class WhatsAppSupportReplyDto
{
    public string ToPhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}