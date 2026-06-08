using System;
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.Dtos.Tickets;

/// <summary>
/// Technician submits a report after resolving a ticket
/// </summary>
// public class CreateTicketReportDto
// {
//     [Required]
//     [MinLength(10, ErrorMessage = "Work summary must be at least 10 characters")]
//     public string WorkSummary { get; set; } = string.Empty;

//     [Required]
//     [MinLength(10, ErrorMessage = "Resolution steps must be at least 10 characters")]
//     public string ResolutionSteps { get; set; } = string.Empty;

//     public string? PartsUsed { get; set; }

//     [Required]
//     [RegularExpression(@"^\d+h?\s*\d*m?$", ErrorMessage = "Time spent format: \"2h 30m\" or \"45m\"")]
//     public string TimeSpent { get; set; } = string.Empty;

//     public string? Recommendations { get; set; }

//     public bool IsResolved { get; set; } = true;

//     public string? FollowUpNotes { get; set; }
// }

public class CreateTicketReportDto
{
    [Required(ErrorMessage = "Work summary is required")]
    public string WorkSummary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Resolution steps are required")]
    public string ResolutionSteps { get; set; } = string.Empty;

    public string? PartsUsed { get; set; }

    [Required(ErrorMessage = "Time spent is required")]
    public string TimeSpent { get; set; } = string.Empty;

    public string? Recommendations { get; set; }

    [Required]
    public bool IsResolved { get; set; } = true;

    public string? FollowUpNotes { get; set; }
}

/// <summary>
/// Response DTO when viewing a ticket report
/// </summary>
public class TicketReportResponseDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public string WorkSummary { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    public string PartsUsed { get; set; } = string.Empty;
    public string TimeSpent { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public string? FollowUpNotes { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for downloading a ticket as PDF
/// Contains all ticket info needed for the PDF document
/// </summary>
public class TicketPdfDto
{
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string AssignedToUserName { get; set; } = string.Empty;
    public string? AssignedByUserName { get; set; }
    public string? ClosedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<TicketCommentPdfDto> Comments { get; set; } = new();
    public List<TicketActivityPdfDto> ActivityLogs { get; set; } = new();
    public TicketReportPdfDto? Report { get; set; }
}

public class TicketCommentPdfDto
{
    public string UserName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TicketActivityPdfDto
{
    public string Action { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string? ChangedByUserName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class TicketReportPdfDto
{
    public string TechnicianName { get; set; } = string.Empty;
    public string WorkSummary { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    public string PartsUsed { get; set; } = string.Empty;
    public string TimeSpent { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public string? FollowUpNotes { get; set; }
    public DateTime SubmittedAt { get; set; }
}