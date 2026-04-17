using System;
using System.Collections.Generic;

namespace TicketingSystem.Api.Models;

public class Tickets
{
    // Primary Key
    public Guid Id { get; set; }
    
    // Properties
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Foreign Keys (link to Users table)
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }      
    public Guid? AssignedByUserId { get; set; }    
    public Guid? ClosedByUserId { get; set; }   
    
    // Status & Priority
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public Priority Priority { get; set; } = Priority.Medium;
    
   
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    // The user who created this ticket (ALWAYS exists)
    public Users CreatedByUser { get; set; } = null!;
    
    // The user this ticket is assigned to (might not exist yet)
    public Users? AssignedToUser { get; set; }
    
    // The user who assigned this ticket (might not exist yet)
    public Users? AssignedByUser { get; set; }
    // The user who closed this ticket (might not exist yet)
    public Users? ClosedByUser { get; set; }
    
    // All comments on this ticket
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    
    // All activity logs for this ticket
    public ICollection<TicketActivityLog> ActivityLogs { get; set; } = new List<TicketActivityLog>();
}