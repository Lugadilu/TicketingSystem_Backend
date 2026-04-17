using System;

namespace TicketingSystem.Api.Dtos.Auth;

public class ForgotPasswordResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = "Reset password link sent to your email";
}