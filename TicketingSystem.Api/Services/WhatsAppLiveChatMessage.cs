namespace TicketingSystem.Api.Services;

public sealed record WhatsAppLiveChatMessage(
    Guid ConversationId,
    Guid MessageId,
    string FromPhoneNumber,
    string CustomerName,
    string Message,
    string Direction,
    DateTime CreatedAtUtc);




// namespace TicketingSystem.Api.Services;

// public sealed record WhatsAppLiveChatMessage(
//     string FromPhoneNumber,
//     string CustomerName,
//     string Message,
//     DateTime ReceivedAtUtc);