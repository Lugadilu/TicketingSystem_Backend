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

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

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

    // Safaricom sandbox is known to randomly return empty 400 responses on the token
    // endpoint even with correct credentials. We retry up to 3 times with a short
    // delay before giving up. This handles the flakiness transparently.
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
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

                    _cachedToken = token;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(3540);

                    _logger.LogInformation(
                        "M-Pesa access token cached. Expires at {Expiry} UTC", _tokenExpiry);

                    return _cachedToken;
                }

                // Empty body 400 = Safaricom sandbox flakiness — retry after delay
                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(attempt * 2); // 2s, 4s
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
            Amount = amount.ToString(),
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

        request.Headers.Add("User-Agent", "TicketingSystem/1.0");

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

    public async Task<JsonElement> QueryStkPushAsync(
        string checkoutRequestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkoutRequestId))
            throw new InvalidOperationException("CheckoutRequestId is required.");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var timestamp = GenerateTimestamp();
        var password = GeneratePassword(timestamp);

        var payload = new
        {
            BusinessShortCode = _options.ShortCode,
            Password = password,
            Timestamp = timestamp,
            CheckoutRequestID = checkoutRequestId
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/mpesa/stkpushquery/v1/query");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("User-Agent", "TicketingSystem/1.0");

        request.Content = JsonContent.Create(payload);

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






// using System.Net.Http.Headers;
// using System.Net.Http.Json;
// using System.Text;
// using System.Text.Json;
// using Microsoft.Extensions.Options;

// namespace TicketingSystem.Api.Services;

// public sealed class MpesaDarajaService : IMpesaDarajaService
// {
//     private readonly HttpClient _httpClient;
//     private readonly MpesaDarajaOptions _options;
//     private readonly IPhoneNumberService _phoneNumberService;
//     private readonly ILogger<MpesaDarajaService> _logger;

//     private string? _cachedToken;
//     private DateTime _tokenExpiry = DateTime.MinValue;
//     private readonly SemaphoreSlim _tokenLock = new(1, 1);

//     public MpesaDarajaService(
//         HttpClient httpClient,
//         IOptions<MpesaDarajaOptions> options,
//         IPhoneNumberService phoneNumberService,
//         ILogger<MpesaDarajaService> logger)
//     {
//         _httpClient = httpClient;
//         _options = options.Value;
//         _phoneNumberService = phoneNumberService;
//         _logger = logger;
//     }

//     private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
//     {
//         if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
//             return _cachedToken;

//         await _tokenLock.WaitAsync(cancellationToken);
//         try
//         {
//             if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
//                 return _cachedToken;

//             var credentials = Convert.ToBase64String(
//                 Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));

//             using var request = new HttpRequestMessage(
//                 HttpMethod.Get,
//                 "/oauth/v1/generate?grant_type=client_credentials");

//             request.Headers.Authorization =
//                 new AuthenticationHeaderValue("Basic", credentials);

//             // Some Safaricom sandbox nodes reject requests without a User-Agent header
//             request.Headers.Add("User-Agent", "TicketingSystem/1.0");

//             _logger.LogInformation(
//                 "Fetching fresh M-Pesa access token from {BaseUrl}",
//                 _options.BaseUrl);

//             var response = await _httpClient.SendAsync(request, cancellationToken);
//             var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//             _logger.LogInformation(
//                 "Token response: {StatusCode} - {Body}",
//                 response.StatusCode,
//                 responseBody);

//             if (!response.IsSuccessStatusCode)
//             {
//                 _logger.LogError(
//                     "M-Pesa access token request failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                     response.StatusCode,
//                     responseBody);

//                 throw new InvalidOperationException("M-Pesa access token request failed.");
//             }

//             using var document = JsonDocument.Parse(responseBody);

//             var token = document.RootElement.GetProperty("access_token").GetString()
//                 ?? throw new InvalidOperationException("M-Pesa access token missing in response.");

//             _cachedToken = token;
//             _tokenExpiry = DateTime.UtcNow.AddSeconds(3540);

//             _logger.LogInformation("M-Pesa access token cached. Expires at {Expiry} UTC", _tokenExpiry);

//             return _cachedToken;
//         }
//         finally
//         {
//             _tokenLock.Release();
//         }
//     }

//     private static string GenerateTimestamp()
//     {
//         return DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
//     }

//     private string GeneratePassword(string timestamp)
//     {
//         var rawPassword = $"{_options.ShortCode}{_options.PassKey}{timestamp}";

//         return Convert.ToBase64String(
//             Encoding.UTF8.GetBytes(rawPassword));
//     }

//     public async Task<JsonElement> StartStkPushAsync(
//         string phoneNumber,
//         int amount,
//         string accountReference,
//         string transactionDescription,
//         CancellationToken cancellationToken = default)
//     {
//         var normalizedPhoneNumber = _phoneNumberService.NormalizeKenyanMobile(phoneNumber);

//         if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
//             throw new InvalidOperationException("Invalid M-Pesa phone number.");

//         if (amount <= 0)
//             throw new InvalidOperationException("Amount must be greater than zero.");

//         var accessToken = await GetAccessTokenAsync(cancellationToken);
//         var timestamp = GenerateTimestamp();
//         var password = GeneratePassword(timestamp);

//         var payload = new
//         {
//             BusinessShortCode = _options.ShortCode,
//             Password = password,
//             Timestamp = timestamp,
//             TransactionType = _options.TransactionType,
//             Amount = amount.ToString(),
//             PartyA = normalizedPhoneNumber,
//             PartyB = _options.ShortCode,
//             PhoneNumber = normalizedPhoneNumber,
//             CallBackURL = _options.CallbackUrl,
//             AccountReference = accountReference,
//             TransactionDesc = transactionDescription
//         };

//         using var request = new HttpRequestMessage(
//             HttpMethod.Post,
//             "/mpesa/stkpush/v1/processrequest");

//         request.Headers.Authorization =
//             new AuthenticationHeaderValue("Bearer", accessToken);

//         request.Headers.Add("User-Agent", "TicketingSystem/1.0");

//         request.Content = JsonContent.Create(payload);

//         var response = await _httpClient.SendAsync(request, cancellationToken);
//         var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//         if (!response.IsSuccessStatusCode)
//         {
//             _logger.LogError(
//                 "M-Pesa STK Push failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                 response.StatusCode,
//                 responseBody);

//             throw new InvalidOperationException("M-Pesa STK Push request failed.");
//         }

//         _logger.LogInformation(
//             "M-Pesa STK Push request accepted. PhoneNumber: {PhoneNumber}. Response: {ResponseBody}",
//             normalizedPhoneNumber,
//             responseBody);

//         using var document = JsonDocument.Parse(responseBody);
//         return document.RootElement.Clone();
//     }

//     public async Task<JsonElement> QueryStkPushAsync(
//         string checkoutRequestId,
//         CancellationToken cancellationToken = default)
//     {
//         if (string.IsNullOrWhiteSpace(checkoutRequestId))
//             throw new InvalidOperationException("CheckoutRequestId is required.");

//         var accessToken = await GetAccessTokenAsync(cancellationToken);
//         var timestamp = GenerateTimestamp();
//         var password = GeneratePassword(timestamp);

//         var payload = new
//         {
//             BusinessShortCode = _options.ShortCode,
//             Password = password,
//             Timestamp = timestamp,
//             CheckoutRequestID = checkoutRequestId
//         };

//         using var request = new HttpRequestMessage(
//             HttpMethod.Post,
//             "/mpesa/stkpushquery/v1/query");

//         request.Headers.Authorization =
//             new AuthenticationHeaderValue("Bearer", accessToken);

//         request.Headers.Add("User-Agent", "TicketingSystem/1.0");

//         request.Content = JsonContent.Create(payload);

//         var response = await _httpClient.SendAsync(request, cancellationToken);
//         var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//         if (!response.IsSuccessStatusCode)
//         {
//             _logger.LogError(
//                 "M-Pesa STK Query failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                 response.StatusCode,
//                 responseBody);

//             throw new InvalidOperationException("M-Pesa STK Query request failed.");
//         }

//         _logger.LogInformation(
//             "M-Pesa STK Query successful. CheckoutRequestId: {CheckoutRequestId}. Response: {ResponseBody}",
//             checkoutRequestId,
//             responseBody);

//         using var document = JsonDocument.Parse(responseBody);
//         return document.RootElement.Clone();
//     }
// }







// using System.Net.Http.Headers;
// using System.Net.Http.Json;
// using System.Text;
// using System.Text.Json;
// using Microsoft.Extensions.Options;

// namespace TicketingSystem.Api.Services;

// public sealed class MpesaDarajaService : IMpesaDarajaService
// {
//     private readonly HttpClient _httpClient;
//     private readonly MpesaDarajaOptions _options;
//     private readonly IPhoneNumberService _phoneNumberService;
//     private readonly ILogger<MpesaDarajaService> _logger;

//     // Token cache — avoids fetching a new token on every request.
//     // Daraja tokens are valid for 3600 seconds (1 hour).
//     // We store the token and its expiry, and only fetch a new one when it has expired.
//     // SemaphoreSlim ensures only one token fetch happens at a time if two requests
//     // arrive simultaneously with an expired token.
//     private string? _cachedToken;
//     private DateTime _tokenExpiry = DateTime.MinValue;
//     private readonly SemaphoreSlim _tokenLock = new(1, 1);

//     public MpesaDarajaService(
//         HttpClient httpClient,
//         IOptions<MpesaDarajaOptions> options,
//         IPhoneNumberService phoneNumberService,
//         ILogger<MpesaDarajaService> logger)
//     {
//         _httpClient = httpClient;
//         _options = options.Value;
//         _phoneNumberService = phoneNumberService;
//         _logger = logger;
//     }

//     // Returns a valid token from cache, or fetches a fresh one if expired.
//     // We subtract 60 seconds from the expiry as a safety buffer so we never
//     // use a token that is about to expire mid-request.
//     private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
//     {
//         // Fast path — return cached token if still valid (outside lock for performance)
//         if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
//             return _cachedToken;

//         // Slow path — only one thread fetches at a time
//         await _tokenLock.WaitAsync(cancellationToken);
//         try
//         {
//             // Re-check inside the lock in case another thread already refreshed
//             if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
//                 return _cachedToken;

//             var credentials = Convert.ToBase64String(
//                 Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));

//             using var request = new HttpRequestMessage(
//                 HttpMethod.Get,
//                 "/oauth/v1/generate?grant_type=client_credentials");

//             request.Headers.Authorization =
//                 new AuthenticationHeaderValue("Basic", credentials);

//             _logger.LogInformation(
//                 "Fetching fresh M-Pesa access token from {BaseUrl}",
//                 _options.BaseUrl);

//             var response = await _httpClient.SendAsync(request, cancellationToken);
//             var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//             _logger.LogInformation(
//                 "Token response: {StatusCode} - {Body}",
//                 response.StatusCode,
//                 responseBody);

//             if (!response.IsSuccessStatusCode)
//             {
//                 _logger.LogError(
//                     "M-Pesa access token request failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                     response.StatusCode,
//                     responseBody);

//                 throw new InvalidOperationException("M-Pesa access token request failed.");
//             }

//             using var document = JsonDocument.Parse(responseBody);

//             var token = document.RootElement.GetProperty("access_token").GetString()
//                 ?? throw new InvalidOperationException("M-Pesa access token missing in response.");

//             // Cache for 3600s minus 60s safety buffer
//             _cachedToken = token;
//             _tokenExpiry = DateTime.UtcNow.AddSeconds(3540);

//             _logger.LogInformation("M-Pesa access token cached. Expires at {Expiry} UTC", _tokenExpiry);

//             return _cachedToken;
//         }
//         finally
//         {
//             _tokenLock.Release();
//         }
//     }

//     // Daraja requires the timestamp in EAT (UTC+3) format: yyyyMMddHHmmss.
//     // It is used both as part of the request and as an ingredient in the password hash.
//     private static string GenerateTimestamp()
//     {
//         return DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
//     }

//     // Password = Base64(ShortCode + PassKey + Timestamp).
//     // Daraja uses this to verify the request came from a trusted source that knows the PassKey.
//     private string GeneratePassword(string timestamp)
//     {
//         var rawPassword = $"{_options.ShortCode}{_options.PassKey}{timestamp}";

//         return Convert.ToBase64String(
//             Encoding.UTF8.GetBytes(rawPassword));
//     }

//     // Initiates the STK Push — sends a payment prompt to the customer's phone.
//     // Daraja returns immediately with an acceptance acknowledgement.
//     // The actual payment result arrives later via the CallbackUrl.
//     public async Task<JsonElement> StartStkPushAsync(
//         string phoneNumber,
//         int amount,
//         string accountReference,
//         string transactionDescription,
//         CancellationToken cancellationToken = default)
//     {
//         var normalizedPhoneNumber = _phoneNumberService.NormalizeKenyanMobile(phoneNumber);

//         if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
//             throw new InvalidOperationException("Invalid M-Pesa phone number.");

//         if (amount <= 0)
//             throw new InvalidOperationException("Amount must be greater than zero.");

//         var accessToken = await GetAccessTokenAsync(cancellationToken);
//         //var timestamp = GenerateTimestamp();
//         //var password = GeneratePassword(timestamp);
//         var timestamp = GenerateTimestamp();
// var password = GeneratePassword(timestamp);

// _logger.LogInformation(
//     "STK payload — ShortCode: '{ShortCode}', Timestamp: '{Timestamp}', PasswordLength: {PasswordLength}",
//     _options.ShortCode,
//     timestamp,
//     password.Length);

        

//         var payload = new
//         {
//             BusinessShortCode = _options.ShortCode,
//             Password = password,
//             Timestamp = timestamp,
//             TransactionType = _options.TransactionType,
//             //Amount = amount,
//             Amount = amount.ToString(),
//             PartyA = normalizedPhoneNumber,
//             PartyB = _options.ShortCode,
//             PhoneNumber = normalizedPhoneNumber,
//             CallBackURL = _options.CallbackUrl,
//             AccountReference = accountReference,
//             TransactionDesc = transactionDescription
//         };

//         using var request = new HttpRequestMessage(
//             HttpMethod.Post,
//             "/mpesa/stkpush/v1/processrequest");

//         request.Headers.Authorization =
//             new AuthenticationHeaderValue("Bearer", accessToken);

//         request.Content = JsonContent.Create(payload);

//         var response = await _httpClient.SendAsync(request, cancellationToken);
//         var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//         if (!response.IsSuccessStatusCode)
//         {
//             _logger.LogError(
//                 "M-Pesa STK Push failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                 response.StatusCode,
//                 responseBody);

//             throw new InvalidOperationException("M-Pesa STK Push request failed.");
//         }

//         _logger.LogInformation(
//             "M-Pesa STK Push request accepted. PhoneNumber: {PhoneNumber}. Response: {ResponseBody}",
//             normalizedPhoneNumber,
//             responseBody);

//         using var document = JsonDocument.Parse(responseBody);
//         return document.RootElement.Clone();
//     }

//     // Queries the status of a previously initiated STK Push using its CheckoutRequestId.
//     // Useful when the callback has not arrived yet, e.g. after a timeout or to let
//     // the user manually check their payment status.
//     public async Task<JsonElement> QueryStkPushAsync(
//         string checkoutRequestId,
//         CancellationToken cancellationToken = default)
//     {
//         if (string.IsNullOrWhiteSpace(checkoutRequestId))
//             throw new InvalidOperationException("CheckoutRequestId is required.");

//         var accessToken = await GetAccessTokenAsync(cancellationToken);
//         var timestamp = GenerateTimestamp();
//         var password = GeneratePassword(timestamp);

//         var payload = new
//         {
//             BusinessShortCode = _options.ShortCode,
//             Password = password,
//             Timestamp = timestamp,
//             CheckoutRequestID = checkoutRequestId
//         };

//         using var request = new HttpRequestMessage(
//             HttpMethod.Post,
//             "/mpesa/stkpushquery/v1/query");

//         request.Headers.Authorization =
//             new AuthenticationHeaderValue("Bearer", accessToken);

//         request.Content = JsonContent.Create(payload);

//         var response = await _httpClient.SendAsync(request, cancellationToken);
//         var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

//         if (!response.IsSuccessStatusCode)
//         {
//             _logger.LogError(
//                 "M-Pesa STK Query failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
//                 response.StatusCode,
//                 responseBody);

//             throw new InvalidOperationException("M-Pesa STK Query request failed.");
//         }

//         _logger.LogInformation(
//             "M-Pesa STK Query successful. CheckoutRequestId: {CheckoutRequestId}. Response: {ResponseBody}",
//             checkoutRequestId,
//             responseBody);

//         using var document = JsonDocument.Parse(responseBody);
//         return document.RootElement.Clone();
//     }
// }





