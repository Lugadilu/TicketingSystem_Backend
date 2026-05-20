using System;

namespace TicketingSystem.Api.Dtos.Auth;

public class RegisterResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Message { get; set; } = 
    "Registration successful. Please verify your email with the OTP sent to your email address. OTP expires in 10 minutes.";
}
