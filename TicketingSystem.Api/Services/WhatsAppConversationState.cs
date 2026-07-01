using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public enum WhatsAppConversationStep
{
    Idle = 1,
    AwaitingTicketId = 2,
    AwaitingNewTicketDescription = 3,
    AwaitingAgentIssue = 4,
    HumanHandoff = 5
}

public sealed class WhatsAppConversationState
{
    public WhatsAppConversationStep Step { get; set; } = WhatsAppConversationStep.Idle;
    public Guid? UserId { get; set; }
    public UserRole? Role { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}