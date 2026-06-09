namespace TicketingSystem.Api.Models;

public class NotificationLog
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}