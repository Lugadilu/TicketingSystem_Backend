using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Dtos.Team;

public class InviteTechnicianResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Message { get; set; } = 
        "Technician invitation sent successfully. They must accept the invitation and set their password to join your team.";
}