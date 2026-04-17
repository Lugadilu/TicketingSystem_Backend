namespace TicketingSystem.Api.Dtos.Admin;

public class CreateTeamLeadResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Message { get; set; } = 
        "TeamLead invitation sent successfully. They must accept the invitation and set their password to become active.";
}