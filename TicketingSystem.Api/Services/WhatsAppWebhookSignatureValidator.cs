using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public interface IWhatsAppWebhookSignatureValidator
{
    bool IsValid(string? signatureHeader, string rawBody);
}

public sealed class WhatsAppWebhookSignatureValidator : IWhatsAppWebhookSignatureValidator
{
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppWebhookSignatureValidator> _logger;

    public WhatsAppWebhookSignatureValidator(
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppWebhookSignatureValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsValid(string? signatureHeader, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret))
        {
            _logger.LogWarning("WhatsApp AppSecret is missing. Webhook signature validation failed.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("WhatsApp signature header is missing.");
            return false;
        }

        const string prefix = "sha256=";

        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("WhatsApp signature header has invalid format.");
            return false;
        }

        var providedSignature = signatureHeader[prefix.Length..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var providedBytes = Encoding.UTF8.GetBytes(providedSignature);
        var computedBytes = Encoding.UTF8.GetBytes(computedSignature);

        if (providedBytes.Length != computedBytes.Length)
        {
            _logger.LogWarning("WhatsApp signature length mismatch.");
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes);
    }
}