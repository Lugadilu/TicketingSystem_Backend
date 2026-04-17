using System;

namespace TicketingSystem.Api.Models;

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
