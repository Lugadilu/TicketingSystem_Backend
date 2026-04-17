using System;

namespace TicketingSystem.Api.Services;

public interface IOtpService
{
    string GenerateOtp();
    DateTime GetOtpExpiryTime();
    bool IsOtpExpired(DateTime? expiryTime);
}

public class OtpService : IOtpService
{
    private const int OtpLength = 6;
    private const int OtpExpiryMinutes = 10;

    public string GenerateOtp()
    {
        var random = new Random();
        return random.Next(0, 999999).ToString("D6");  // Generate 6-digit number
    }

    public DateTime GetOtpExpiryTime()
    {
        return DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
    }

    public bool IsOtpExpired(DateTime? expiryTime)
    {
        if (!expiryTime.HasValue)
            return true;

        return DateTime.UtcNow > expiryTime.Value;
    }
}