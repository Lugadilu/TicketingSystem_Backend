using TicketingSystem.Api.Models;

public class TicketDocument
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property to the related ticket
    public Tickets Ticket { get; set; } = null!;
}