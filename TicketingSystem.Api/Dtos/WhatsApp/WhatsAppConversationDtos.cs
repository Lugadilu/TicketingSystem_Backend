namespace TicketingSystem.Api.Dtos.WhatsApp;

public sealed class WhatsAppConversationListItemDto
{
    public Guid ConversationId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
}

public sealed class WhatsAppConversationMessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public Guid? SenderUserId { get; set; }
    public string? SenderName { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}