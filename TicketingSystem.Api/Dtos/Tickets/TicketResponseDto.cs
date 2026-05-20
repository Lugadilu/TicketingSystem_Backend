using System;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.DTOs.Tickets;

public class TicketResponseDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public Priority Priority { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    
    public Guid? AssignedToUserId { get; set; }
    public string AssignedToUserName { get; set; } = "Unassigned";
    
    public Guid? AssignedByUserId { get; set; }
    public string? AssignedByUserName { get; set; }
    
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    
    public int CommentCount { get; set; }
    public int ActivityLogCount { get; set; }
}
