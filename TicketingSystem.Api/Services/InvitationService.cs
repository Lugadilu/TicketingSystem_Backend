using System;
using System.Security.Cryptography;
using System.Text;

namespace TicketingSystem.Api.Services;

public interface IInvitationService
{
    string GenerateInvitationToken();
    DateTime GetInvitationExpiryTime();
    bool IsInvitationExpired(DateTime? expiryTime);
}

public class InvitationService : IInvitationService
{
    private const int InvitationExpiryDays = 7;  // Invitation expires in 7 days

    public string GenerateInvitationToken()
    {
        // Generate random token
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }

    public DateTime GetInvitationExpiryTime()
    {
        return DateTime.UtcNow.AddDays(InvitationExpiryDays);
    }

    public bool IsInvitationExpired(DateTime? expiryTime)
    {
        if (!expiryTime.HasValue)
            return true;

        return DateTime.UtcNow > expiryTime.Value;
    }
}