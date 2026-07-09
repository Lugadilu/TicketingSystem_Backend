namespace TicketingSystem.Api.Models;

public sealed class CustomerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    public Users? User { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ChannelIdentity> ChannelIdentities { get; set; } = [];
}