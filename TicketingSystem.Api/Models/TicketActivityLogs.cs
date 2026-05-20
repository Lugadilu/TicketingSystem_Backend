using System;

namespace TicketingSystem.Api.Models;

public class TicketActivityLog
{
    public Guid Id { get; set; }
    
    // Foreign Keys
    public Guid TicketId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    
    // Properties
    public string Action { get; set; } = string.Empty;      // "Status Changed", "Assigned", "Commented", etc.
    public string OldValue { get; set; } = string.Empty;    // Old value (e.g., "Open")
    public string NewValue { get; set; } = string.Empty;    // New value (e.g., "InProgress")
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    
    // The ticket that was changed
    public Tickets Ticket { get; set; } = null!;
    
    // The user who made the change
    public Users? ChangedByUser { get; set; } = null!;
}