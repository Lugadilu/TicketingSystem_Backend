using System;

namespace TicketingSystem.Api.Models;

public class TicketAttachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UploadedByUserId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Tickets Ticket { get; set; } = null!;
    public Users UploadedByUser { get; set; } = null!;
}
