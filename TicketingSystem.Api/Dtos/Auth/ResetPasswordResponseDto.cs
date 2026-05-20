using System;

namespace TicketingSystem.Api.Dtos.Auth;

public class ResetPasswordResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = "Password reset successfully. You can now login with your new password.";
}