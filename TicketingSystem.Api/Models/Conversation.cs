namespace TicketingSystem.Api.Models;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;

    public ConversationChannel Channel { get; set; } = ConversationChannel.WhatsApp;
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    public Guid? AssignedAgentUserId { get; set; }
    public Users? AssignedAgentUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMessage> Messages { get; set; } = [];
    public ICollection<ChannelIdentity> ChannelIdentities { get; set; } = [];
}