using System;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.DTOs.Tickets;

public class TicketListItemDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public Priority Priority { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public string? AssignedToUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
