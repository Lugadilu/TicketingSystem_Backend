using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface IWhatsAppUserResolver
{
    Task<Users?> ResolveByWhatsAppPhoneAsync(
        string fromPhoneNumber,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppUserResolver : IWhatsAppUserResolver
{
    private readonly AppDbContext _context;
    private readonly IPhoneNumberService _phoneNumberService;

    public WhatsAppUserResolver(AppDbContext context, IPhoneNumberService phoneNumberService)
    {
        _context = context;
        _phoneNumberService = phoneNumberService;
    }

    public async Task<Users?> ResolveByWhatsAppPhoneAsync(
        string fromPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = _phoneNumberService.NormalizeKenyanMobile(fromPhoneNumber);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var candidates = await _context.Users
            .Where(u => u.IsActive && u.PhoneNumber != string.Empty)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(u =>
            _phoneNumberService.PhonesMatch(u.PhoneNumber, fromPhoneNumber));
    }
}
