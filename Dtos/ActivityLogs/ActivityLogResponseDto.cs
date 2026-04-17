using System;

namespace TicketingSystem.Api.DTOs.ActivityLogs;

public class ActivityLogResponseDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
