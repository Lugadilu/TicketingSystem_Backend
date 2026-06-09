using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public sealed record TicketSmsNotification(
    Guid TicketId,
    string TicketNumber,
    string Title,
    string CustomerFirstName,
    string CustomerPhoneNumber,
    TicketStatus? OldStatus,
    TicketStatus NewStatus,
    string EventKey);