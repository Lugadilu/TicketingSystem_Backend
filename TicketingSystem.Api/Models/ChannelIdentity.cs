namespace TicketingSystem.Api.Models;

public sealed class ChannelIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public ConversationChannel Channel { get; set; } = ConversationChannel.WhatsApp;

    public string ExternalId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}