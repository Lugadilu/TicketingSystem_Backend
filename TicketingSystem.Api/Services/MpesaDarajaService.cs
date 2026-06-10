using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public sealed class MpesaDarajaService : IMpesaDarajaService
{
    private readonly HttpClient _httpClient;
    private readonly MpesaDarajaOptions _options;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ILogger<MpesaDarajaService> _logger;

    public MpesaDarajaService(
        HttpClient httpClient,
        IOptions<MpesaDarajaOptions> options,
        IPhoneNumberService phoneNumberService,
        ILogger<MpesaDarajaService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _phoneNumberService = phoneNumberService;
        _logger = logger;
    }
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/oauth/v1/generate?grant_type=client_credentials");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "M-Pesa access token request failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("M-Pesa access token request failed.");
        }

        using var document = JsonDocument.Parse(responseBody);

        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("M-Pesa access token missing in response.");
    }

    // Generates a timestamp in the format required by M-Pesa (yyyyMMddHHmmss) in East Africa Time
    //  (UTC+3) Daraja STK Push requires timestamo and password for authentication.
    //  The timestamp is used to generate the password and must be in the format yyyyMMddHHmmss 
    // in East Africa Time (UTC+3). This method generates the current timestamp in the required format.

    private static string GenerateTimestamp()
    {
        return DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
    }

    private string GeneratePassword(string timestamp)
    {
        var rawPassword = $"{_options.ShortCode}{_options.PassKey}{timestamp}";

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(rawPassword));
    }

    // This method initiates an M-Pesa STK Push transaction. It takes the customer's phone number,
    //  the amount to be charged, an account reference, and a transaction description.
    //  It normalizes the phone number, obtains an access token, generates the required timestamp
    //  and password, and sends a POST request to the M-Pesa API to initiate the STK Push. 
    // The response from M-Pesa is logged and returned as a JsonElement for further processing by the caller.
    public async Task<JsonElement> StartStkPushAsync(
    string phoneNumber,
    int amount,
    string accountReference,
    string transactionDescription,
    CancellationToken cancellationToken = default)
    {
        var normalizedPhoneNumber = _phoneNumberService.NormalizeKenyanMobile(phoneNumber);

        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
            throw new InvalidOperationException("Invalid M-Pesa phone number.");

        if (amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var timestamp = GenerateTimestamp();
        var password = GeneratePassword(timestamp);

        var payload = new
        {
            BusinessShortCode = _options.ShortCode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = _options.TransactionType,
            Amount = amount,
            PartyA = normalizedPhoneNumber,
            PartyB = _options.ShortCode,
            PhoneNumber = normalizedPhoneNumber,
            CallBackURL = _options.CallbackUrl,
            AccountReference = accountReference,
            TransactionDesc = transactionDescription
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/mpesa/stkpush/v1/processrequest");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "M-Pesa STK Push failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("M-Pesa STK Push request failed.");
        }

        _logger.LogInformation(
            "M-Pesa STK Push request accepted. PhoneNumber: {PhoneNumber}. Response: {ResponseBody}",
            normalizedPhoneNumber,
            responseBody);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }
}