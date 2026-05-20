using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.Dtos.Admin;
/// DTO for SuperAdmin to create and invite a TeamLead.
/// SuperAdmin only - creates user invitation that TeamLead must accept.

public class CreateTeamLeadDto
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

    [MaxLength(500)]
    public string? InvitationMessage { get; set; }
}