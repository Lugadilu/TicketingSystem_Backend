using System;

namespace TicketingSystem.Api.Dtos.Auth;

public class VerifyOtpResponseDto
{
    public bool IsVerified { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
