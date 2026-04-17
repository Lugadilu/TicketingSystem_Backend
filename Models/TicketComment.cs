using System;

namespace TicketingSystem.Api.Models;

public class TicketComment
{
    // Primary Key
    public Guid Id { get; set; }
    
    // Foreign Keys
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    
    // Properties
    public string Comment { get; set; } = string.Empty;
    public string AttachmentUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

   
    // The ticket this comment belongs to
    public Tickets Ticket { get; set; } = null!;
    
    // The user who wrote this comment
    public Users User { get; set; } = null!;
}