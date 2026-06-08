using System;

namespace TicketingSystem.Api.Models;

public class TicketReport
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianUserId { get; set; }

    /// <summary>
    /// Summary of the work performed by the technician
    /// </summary>
    public string WorkSummary { get; set; } = string.Empty;

    /// <summary>
    /// Steps taken to resolve the issue
    /// </summary>
    public string ResolutionSteps { get; set; } = string.Empty;

    /// <summary>
    /// Parts or tools used during resolution
    /// </summary>
    public string PartsUsed { get; set; } = string.Empty;

    /// <summary>
    /// Time spent on the ticket (e.g., "2h 30m")
    /// </summary>
    public string TimeSpent { get; set; } = string.Empty;

    /// <summary>
    /// Recommendations for preventing future issues
    /// </summary>
    public string Recommendations { get; set; } = string.Empty;

    /// <summary>
    /// Whether the issue is fully resolved or requires follow-up
    /// </summary>
    public bool IsResolved { get; set; } = true;

    /// <summary>
    /// Follow-up notes if the issue is not fully resolved
    /// </summary>
    public string? FollowUpNotes { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Tickets Ticket { get; set; } = null!;
    public Users TechnicianUser { get; set; } = null!;
}