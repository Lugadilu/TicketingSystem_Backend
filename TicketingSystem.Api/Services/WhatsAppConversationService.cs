using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Tickets;
using TicketingSystem.Api.DTOs.Tickets;
using TicketingSystem.Api.Models;
using Microsoft.AspNetCore.SignalR;
using TicketingSystem.Api.Hubs;
namespace TicketingSystem.Api.Services;

public interface IWhatsAppConversationService
{
    Task HandleIncomingMessageAsync(
        WhatsAppIncomingMessage message,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppConversationService : IWhatsAppConversationService
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _context;
    private readonly ITicketService _ticketService;
    private readonly IWhatsAppSenderService _sender;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppConversationService> _logger;
    private readonly IHubContext<WhatsAppLiveChatHub> _liveChatHub;

    public WhatsAppConversationService(
        IMemoryCache cache,
        AppDbContext context,
        ITicketService ticketService,
        IWhatsAppSenderService sender,
        IPhoneNumberService phoneNumberService,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppConversationService> logger,
        IHubContext<WhatsAppLiveChatHub> liveChatHub)
    {
        _cache = cache;
        _context = context;
        _ticketService = ticketService;
        _sender = sender;
        _phoneNumberService = phoneNumberService;
        _options = options.Value;
        _logger = logger;
        _liveChatHub = liveChatHub;
    }

    public async Task HandleIncomingMessageAsync(
        WhatsAppIncomingMessage message,
        CancellationToken cancellationToken = default)
    {
        var text = message.Text.Trim();
        var normalizedPhone = _phoneNumberService.NormalizeKenyanMobile(message.FromPhoneNumber);

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Sorry, we could not read your phone number correctly.",
                cancellationToken);

            return;
        }

        var state = GetState(normalizedPhone);

        if (IsHelpIntent(text))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.Idle);
            await _sender.SendMainMenuAsync(message.FromPhoneNumber, cancellationToken);
            return;
        }

        if (IsAgentIntent(text) || text.Equals("talk_to_agent", StringComparison.OrdinalIgnoreCase))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.AwaitingAgentIssue);

            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Talk to an Agent\n\nPlease describe your issue briefly. An agent will see your message.",
                cancellationToken);

            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingAgentIssue)
        {
            SetState(normalizedPhone, WhatsAppConversationStep.HumanHandoff);

            var liveChatMessage = new WhatsAppLiveChatMessage(
                message.FromPhoneNumber,
                message.CustomerName,
                text,
                DateTime.UtcNow);

            await _liveChatHub.Clients.All.SendAsync(
                "ReceiveWhatsAppMessage",
                liveChatMessage,
                cancellationToken);

            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Ticket for human support created. You can continue typing here and an agent will respond shortly.",
                cancellationToken);

            return;
        }

        if (state.Step == WhatsAppConversationStep.HumanHandoff)
            {
                if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
                {
                    SetState(normalizedPhone, WhatsAppConversationStep.Idle);

                    await _sender.SendTextMessageAsync(
                        message.FromPhoneNumber,
                        "Chat ended. You can type \"menu\" anytime to view services.",
                        cancellationToken);

                    return;
                }

                var liveChatMessage = new WhatsAppLiveChatMessage(
                    message.FromPhoneNumber,
                    message.CustomerName,
                    text,
                    DateTime.UtcNow);

                await _liveChatHub.Clients.All.SendAsync(
                    "ReceiveWhatsAppMessage",
                    liveChatMessage,
                    cancellationToken);

                await _sender.SendTextMessageAsync(
                    message.FromPhoneNumber,
                    "Your message has been sent to the support team. Type \"end chat\" to return to the bot.",
                    cancellationToken);

                return;
            }

        if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.Idle);

            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Chat ended. You can type \"menu\" anytime to view services.",
                cancellationToken);

            return;
        }

        if (text.Equals("create_ticket", StringComparison.OrdinalIgnoreCase) || IsCreateTicketIntent(text))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.AwaitingNewTicketDescription);

            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Create Ticket\n\nPlease describe the issue you need help with.",
                cancellationToken);

            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingNewTicketDescription)
        {
            await CreateTicketFromWhatsAppAsync(
                message,
                normalizedPhone,
                text,
                cancellationToken);

            SetState(normalizedPhone, WhatsAppConversationStep.Idle);
            return;
        }

        if (text.Equals("check_status", StringComparison.OrdinalIgnoreCase) || IsStatusIntent(text))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.AwaitingTicketId);

            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Please enter your ticket number, for example TKT-12345678-1234.",
                cancellationToken);

            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingTicketId)
        {
            await SendTicketStatusAsync(
                message.FromPhoneNumber,
                normalizedPhone,
                text,
                cancellationToken);

            SetState(normalizedPhone, WhatsAppConversationStep.Idle);
            return;
        }

        if (text.Equals("my_tickets", StringComparison.OrdinalIgnoreCase))
        {
            await SendRecentTicketsAsync(
                message.FromPhoneNumber,
                normalizedPhone,
                cancellationToken);

            return;
        }

        if (text.Equals("download_report", StringComparison.OrdinalIgnoreCase))
        {
            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "Please open your customer dashboard to download reports. WhatsApp report download will be added next.",
                cancellationToken);

            return;
        }

        await _sender.SendMainMenuAsync(message.FromPhoneNumber, cancellationToken);
    }

    private async Task CreateTicketFromWhatsAppAsync(
        WhatsAppIncomingMessage message,
        string normalizedPhone,
        string description,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone || u.PhoneNumber == message.FromPhoneNumber, cancellationToken);

        if (user == null)
        {
            await _sender.SendTextMessageAsync(
                message.FromPhoneNumber,
                "We could not find an account linked to this WhatsApp number. Please contact support to link your number.",
                cancellationToken);

            return;
        }

        var dto = new CreateTicketDto
        {
            Title = $"WhatsApp request from {message.CustomerName}",
            Description = description,
            Category = "WhatsApp",
            Priority = Priority.Medium
        };

        var result = await _ticketService.CreateTicketAsync(dto, user.Id);

        await _sender.SendTextMessageAsync(
            message.FromPhoneNumber,
            $"Ticket Created\n\nYour ticket {result.TicketNumber} has been created. You will receive updates here on WhatsApp.",
            cancellationToken);
    }

    private async Task SendTicketStatusAsync(
        string whatsappPhone,
        string normalizedPhone,
        string ticketNumber,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t =>
                t.TicketNumber == ticketNumber &&
                (t.CreatedByUser.PhoneNumber == normalizedPhone || t.CreatedByUser.PhoneNumber == whatsappPhone),
                cancellationToken);

        if (ticket == null)
        {
            await _sender.SendTextMessageAsync(
                whatsappPhone,
                "Ticket not found for this WhatsApp number. Please check the ticket number and try again.",
                cancellationToken);

            return;
        }

        await _sender.SendTextMessageAsync(
            whatsappPhone,
            $"Ticket Status\n\nTicket: {ticket.TicketNumber}\nTitle: {ticket.Title}\nStatus: {ticket.Status}\nPriority: {ticket.Priority}",
            cancellationToken);
    }

    private async Task SendRecentTicketsAsync(
        string whatsappPhone,
        string normalizedPhone,
        CancellationToken cancellationToken)
    {
        var tickets = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Where(t => t.CreatedByUser.PhoneNumber == normalizedPhone || t.CreatedByUser.PhoneNumber == whatsappPhone)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            await _sender.SendTextMessageAsync(
                whatsappPhone,
                "No recent tickets were found for this WhatsApp number.",
                cancellationToken);

            return;
        }

        var lines = tickets.Select(t =>
            $"{t.TicketNumber} - {t.Status} - {t.Title}");

        await _sender.SendTextMessageAsync(
            whatsappPhone,
            "Your Recent Tickets\n\n" + string.Join("\n", lines),
            cancellationToken);
    }

    private WhatsAppConversationState GetState(string phoneNumber)
    {
        var key = GetCacheKey(phoneNumber);

        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes);

            return new WhatsAppConversationState();
        })!;
    }

    private void SetState(string phoneNumber, WhatsAppConversationStep step)
    {
        var key = GetCacheKey(phoneNumber);

        _cache.Set(
            key,
            new WhatsAppConversationState
            {
                Step = step,
                UpdatedAtUtc = DateTime.UtcNow
            },
            TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes));
    }

    private static string GetCacheKey(string phoneNumber)
    {
        return $"whatsapp:conversation:{phoneNumber}";
    }

    private static bool IsHelpIntent(string text)
    {
        return Regex.IsMatch(text, @"\b(help|menu|start|services|hi|hello)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsStatusIntent(string text)
    {
        return Regex.IsMatch(text, @"\b(status|track|check|ticket status)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsCreateTicketIntent(string text)
    {
        return Regex.IsMatch(text, @"\b(create|new ticket|raise ticket|open ticket|ticket)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsAgentIntent(string text)
    {
        return Regex.IsMatch(text, @"\b(agent|human|person|support|representative)\b", RegexOptions.IgnoreCase);
    }
}