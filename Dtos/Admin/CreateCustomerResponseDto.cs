namespace TicketingSystem.Api.Dtos.Admin;

public class CreateCustomerResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Message { get; set; } = 
        "Customer account created successfully. OTP sent to email for verification.";
}