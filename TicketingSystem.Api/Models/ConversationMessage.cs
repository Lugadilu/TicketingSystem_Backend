namespace TicketingSystem.Api.Models;

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public MessageDirection Direction { get; set; }
    public MessageSenderType SenderType { get; set; }

    public Guid? SenderUserId { get; set; }
    public Users? SenderUser { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? ExternalMessageId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}