using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public sealed class MpesaDarajaService : IMpesaDarajaService
{
    private readonly HttpClient _httpClient;
    private readonly MpesaDarajaOptions _options;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ILogger<MpesaDarajaService> _logger;

    // Token cache
    // Daraja access tokens are valid for 3600 seconds (1 hour).
    // Instead of fetching a new token on every request, we cache it and reuse it
    // until it is 60 seconds away from expiry — giving a safe buffer for
    // in-flight requests. SemaphoreSlim(1,1) acts as an async lock so that if
    // two requests arrive simultaneously with an expired token, only one of them
    // fetches a new token while the other waits and then reuses it.
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

  
    // JSON serialization options
   
    // Daraja requires PascalCase JSON keys e.g. "BusinessShortCode", not
    // "businessShortCode". ASP.NET Core apps often configure camelCase globally,
    // so we use our own options with PropertyNamingPolicy = null (PascalCase)
    // and serialize manually instead of using JsonContent.Create, which would
    // silently pick up the global camelCase policy and cause Daraja to reject
    // the request with "Invalid BusinessShortCode".
    private static readonly JsonSerializerOptions PascalCaseOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

  
    // GetAccessTokenAsync
    // Fetches a short-lived OAuth2 bearer token from Daraja using HTTP Basic
    // auth (ConsumerKey:ConsumerSecret encoded in Base64). We use the injected
    // _httpClient whose BaseAddress is already set to options.BaseUrl in
    // Program.cs via AddHttpClient — no need to create a new HttpClient here.
    //
    // Safaricom sandbox occasionally returns an empty 400 with no explanation
    // even when credentials are correct. We retry up to 3 times with a short
    // back-off (2 s, 4 s) before giving up.
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Fast path — return cached token without acquiring the lock
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        // Slow path — acquire lock so only one request fetches at a time
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock: another thread may have refreshed already
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));

                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "/oauth/v1/generate?grant_type=client_credentials");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);

                request.Headers.Add("User-Agent", "TicketingSystem/1.0");

                _logger.LogInformation(
                    "Fetching M-Pesa access token. Attempt {Attempt}/{Max}",
                    attempt, maxAttempts);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation(
                    "Token response: {StatusCode} - {Body}",
                    response.StatusCode,
                    responseBody);

                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(responseBody);

                    var token = document.RootElement.GetProperty("access_token").GetString()
                        ?? throw new InvalidOperationException("M-Pesa access token missing in response.");

                    // Cache for 59 minutes (token lifetime is 60 min)
                    _cachedToken = token;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(3540);

                    _logger.LogInformation(
                        "M-Pesa access token cached. Expires at {Expiry} UTC", _tokenExpiry);

                    return _cachedToken;
                }

                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(attempt * 2); // 2 s, then 4 s
                    _logger.LogWarning(
                        "Token request failed on attempt {Attempt}. Retrying in {Delay}s...",
                        attempt, delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    _logger.LogError(
                        "M-Pesa access token request failed after {Max} attempts. StatusCode: {StatusCode}. Response: {ResponseBody}",
                        maxAttempts, response.StatusCode, responseBody);

                    throw new InvalidOperationException("M-Pesa access token request failed.");
                }
            }

            throw new InvalidOperationException("M-Pesa access token request failed.");
        }
        finally
        {
            _tokenLock.Release();
        }
    }


    // GenerateTimestamp

    // Daraja requires the timestamp in East Africa Time (UTC+3) formatted as
    // yyyyMMddHHmmss. It is used both in the request body and as an ingredient
    // in the password. We add 3 hours to UTC to get EAT.
    private static string GenerateTimestamp()
    {
        return DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
    }


    // GeneratePassword
    // The password is Base64(ShortCode + PassKey + Timestamp).
    // Daraja uses this to verify the request came from a party that knows the
    // PassKey, which is a secret shared between your app and Safaricom.
    private string GeneratePassword(string timestamp)
    {
        var rawPassword = $"{_options.ShortCode}{_options.PassKey}{timestamp}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(rawPassword));
    }

    
    // StartStkPushAsync

    // Sends a payment prompt (STK Push) to the customer's phone via Daraja.
    // Daraja responds immediately with an acceptance acknowledgement — this does
    // NOT mean the customer has paid. The actual payment result (success, cancel,
    // or timeout) arrives later as a POST to your CallbackUrl.
    //
    // The phone number is normalised to Kenyan international format (2547XXXXXXXX)
    // before being sent. An unnormalised number causes Daraja to silently fail.
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

        // We use Dictionary<string, string> instead of an anonymous object so
        // that JSON key names are always exactly what we write here. Anonymous
        // objects serialized with JsonContent.Create inherit the app's global
        // JsonOptions which typically uses camelCase — causing Daraja to reject
        // the request because it expects "BusinessShortCode", not "businessShortCode".
        var payload = new Dictionary<string, string>
        {
            ["BusinessShortCode"] = _options.ShortCode,
            ["Password"]          = password,
            ["Timestamp"]         = timestamp,
            ["TransactionType"]   = _options.TransactionType,
            ["Amount"]            = amount.ToString(),
            ["PartyA"]            = normalizedPhoneNumber,
            ["PartyB"]            = _options.ShortCode,
            ["PhoneNumber"]       = normalizedPhoneNumber,
            ["CallBackURL"]       = _options.CallbackUrl,
            ["AccountReference"]  = accountReference,
            ["TransactionDesc"]   = transactionDescription
        };

        var payloadJson = JsonSerializer.Serialize(payload, PascalCaseOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/mpesa/stkpush/v1/processrequest");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("User-Agent", "TicketingSystem/1.0");

        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

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
            "M-Pesa STK Push accepted. PhoneNumber: {PhoneNumber}. Response: {ResponseBody}",
            normalizedPhoneNumber,
            responseBody);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }


    // QueryStkPushAsync

    // Checks the current status of a previously initiated STK Push using the
    // CheckoutRequestID that was returned by StartStkPushAsync.
    // Use this when the callback has not arrived within a reasonable time, or
    // to let the user manually check their payment status on demand.
    // ResultCode 0 = confirmed paid. Anything else = not yet paid or failed.
    public async Task<JsonElement> QueryStkPushAsync(
        string checkoutRequestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkoutRequestId))
            throw new InvalidOperationException("CheckoutRequestId is required.");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var timestamp = GenerateTimestamp();
        var password = GeneratePassword(timestamp);

        var payload = new Dictionary<string, string>
        {
            ["BusinessShortCode"] = _options.ShortCode,
            ["Password"]          = password,
            ["Timestamp"]         = timestamp,
            ["CheckoutRequestID"] = checkoutRequestId
        };

        var payloadJson = JsonSerializer.Serialize(payload, PascalCaseOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/mpesa/stkpushquery/v1/query");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("User-Agent", "TicketingSystem/1.0");

        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "M-Pesa STK Query failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("M-Pesa STK Query request failed.");
        }

        _logger.LogInformation(
            "M-Pesa STK Query successful. CheckoutRequestId: {CheckoutRequestId}. Response: {ResponseBody}",
            checkoutRequestId,
            responseBody);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }
}



