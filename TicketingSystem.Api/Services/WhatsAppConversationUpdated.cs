namespace TicketingSystem.Api.Services;

public sealed record WhatsAppConversationUpdated(
    Guid ConversationId,
    string FromPhoneNumber,
    string CustomerName,
    string LastMessagePreview,
    string Direction,
    DateTime LastMessageAtUtc);