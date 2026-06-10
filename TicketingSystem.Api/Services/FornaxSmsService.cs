using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public interface IFornaxSmsService
{
    Task SendSmsAsync(string mobile, string message, CancellationToken cancellationToken = default);
}

public sealed class FornaxSmsService : IFornaxSmsService
{
    private readonly HttpClient _httpClient;
    private readonly FornaxSmsOptions _options;
    private readonly ILogger<FornaxSmsService> _logger;
    private readonly IPhoneNumberService _phoneNumberService;

    public FornaxSmsService(
        HttpClient httpClient,
        IOptions<FornaxSmsOptions> options,
        ILogger<FornaxSmsService> logger,
        IPhoneNumberService phoneNumberService)  
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _phoneNumberService = phoneNumberService;
    }

    public async Task SendSmsAsync(
        string mobile,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            _logger.LogWarning("SMS skipped because mobile number is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("SMS skipped because message is empty.");
            return;
        }

        // STEP 1: Normalize the phone number
        var destinationMobile = _phoneNumberService.NormalizeKenyanMobile(mobile);

        // STEP 2: Apply test mode redirection (if enabled)
        if (_options.TestMode)
        {
            if (string.IsNullOrWhiteSpace(_options.TestMobile))
            {
                _logger.LogWarning("SMS test mode is enabled, but Fornax:TestMobile is missing.");
                return;
            }

            // Normalize the test mobile number as well
            var normalizedTestMobile = _phoneNumberService.NormalizeKenyanMobile(_options.TestMobile);

            _logger.LogInformation(
                "SMS test mode enabled. Redirecting SMS from {OriginalMobile} to {TestMobile}.",
                mobile,
                normalizedTestMobile);

            destinationMobile = normalizedTestMobile;
        }

        // STEP 3: Build the payload with the normalized destination mobile
        var payload = new
        {
            apikey = _options.ApiKey,
            partnerID = _options.PartnerId,
            mobile = destinationMobile,  // Now using normalized number
            message,
            shortcode = _options.Shortcode
        };

        var response = await _httpClient.PostAsJsonAsync(
            _options.SendPath,
            payload,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Fornax SMS failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("Fornax SMS request failed.");
        }

        _logger.LogInformation(
            "Fornax SMS sent to {Mobile}. Response: {ResponseBody}",
            destinationMobile,  // Log the actual number that received the SMS
            responseBody);
    }
}

