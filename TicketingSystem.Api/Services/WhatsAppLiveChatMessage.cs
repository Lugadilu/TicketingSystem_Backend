namespace TicketingSystem.Api.Services;

public sealed record WhatsAppLiveChatMessage(
    string FromPhoneNumber,
    string CustomerName,
    string Message,
    DateTime ReceivedAtUtc);