namespace TicketingSystem.Api.Services;

public sealed record WhatsAppIncomingMessage(
    string MessageId,
    string FromPhoneNumber,
    string CustomerName,
    string Text,
    string MessageType,
    DateTime ReceivedAtUtc);