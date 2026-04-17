namespace TicketingSystem.Api.Dtos.Auth;

public class AcceptInvitationResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Message { get; set; } = "Invitation accepted successfully. You can now login.";
}