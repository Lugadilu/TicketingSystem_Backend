using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public interface IWhatsAppSenderService
{
    Task SendTextMessageAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);

    Task SendMainMenuAsync(
        string toPhoneNumber,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppSenderService : IWhatsAppSenderService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppSenderService> _logger;

    public WhatsAppSenderService(
        HttpClient httpClient,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppSenderService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task SendMainMenuAsync(
        string toPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "interactive",
            interactive = new
            {
                type = "list",
                header = new
                {
                    type = "text",
                    text = "Ticketing Services"
                },
                body = new
                {
                    text = "Welcome to Ticketing Support. Choose a service below:"
                },
                footer = new
                {
                    text = "Powered by your ticketing system"
                },
                action = new
                {
                    button = "View Services",
                    sections = new[]
                    {
                        new
                        {
                            title = "Services",
                            rows = new[]
                            {
                                new
                                {
                                    id = "create_ticket",
                                    title = "Create Ticket",
                                    description = "Open a new support ticket"
                                },
                                new
                                {
                                    id = "check_status",
                                    title = "Check Status",
                                    description = "Check an existing ticket"
                                },
                                new
                                {
                                    id = "my_tickets",
                                    title = "My Tickets",
                                    description = "View your recent tickets"
                                },
                                new
                                {
                                    id = "download_report",
                                    title = "Download Report",
                                    description = "Get a completed ticket report"
                                },
                                new
                                {
                                    id = "talk_to_agent",
                                    title = "Talk to Agent",
                                    description = "Chat with a support person"
                                }
                            }
                        }
                    }
                }
            }
        };

        return SendPayloadAsync(toPhoneNumber, payload, cancellationToken);
    }

    public Task SendTextMessageAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "text",
            text = new
            {
                preview_url = false,
                body = message
            }
        };

        return SendPayloadAsync(toPhoneNumber, payload, cancellationToken);
    }

    private async Task SendPayloadAsync(
        string toPhoneNumber,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{_options.PhoneNumberId}/messages");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(
                "WhatsApp API rate limit hit while sending to {PhoneNumber}. Response: {ResponseBody}",
                toPhoneNumber,
                responseBody);

            throw new InvalidOperationException("WhatsApp API rate limit reached.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "WhatsApp send failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("WhatsApp message send failed.");
        }

        _logger.LogInformation(
            "WhatsApp message sent to {PhoneNumber}. Response: {ResponseBody}",
            toPhoneNumber,
            responseBody);
    }
}