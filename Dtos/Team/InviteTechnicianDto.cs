using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Dtos.Team;

/// <summary>
/// DTO for TeamLead to invite a Technician to join their team.
/// TeamLead only - creates user invitation that Technician must accept.
/// Technician will be assigned to this TeamLead after accepting.
/// </summary>
public class InviteTechnicianDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [MinLength(2)]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MinLength(2)]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number")]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    public UserRole Role { get; set; }  // Technician or Support

    [MaxLength(500)]
    public string? InvitationMessage { get; set; }
}