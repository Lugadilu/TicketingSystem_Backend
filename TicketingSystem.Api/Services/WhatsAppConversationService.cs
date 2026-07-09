using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Tickets;
using TicketingSystem.Api.DTOs.Tickets;
using TicketingSystem.Api.Hubs;
using TicketingSystem.Api.Models;

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
    private readonly IWhatsAppUserResolver _userResolver;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IWhatsAppConversationStore _conversationStore;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppConversationService> _logger;
    private readonly IHubContext<WhatsAppLiveChatHub> _liveChatHub;

    public WhatsAppConversationService(
        IMemoryCache cache,
        AppDbContext context,
        ITicketService ticketService,
        IWhatsAppSenderService sender,
        IWhatsAppUserResolver userResolver,
        IPhoneNumberService phoneNumberService,
        IWhatsAppConversationStore conversationStore,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppConversationService> logger,
        IHubContext<WhatsAppLiveChatHub> liveChatHub)
    {
        _cache = cache;
        _context = context;
        _ticketService = ticketService;
        _sender = sender;
        _userResolver = userResolver;
        _phoneNumberService = phoneNumberService;
        _conversationStore = conversationStore;
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

        if (state.Step == WhatsAppConversationStep.HumanHandoff)
        {
            await HandleHumanHandoffAsync(message, normalizedPhone, text, cancellationToken);
            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingAgentIssue)
        {
            await HandleAwaitingAgentIssueAsync(message, normalizedPhone, text, cancellationToken);
            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingNewTicketDescription)
        {
            await CreateTicketFromWhatsAppAsync(message, normalizedPhone, text, cancellationToken);
            SetState(normalizedPhone, WhatsAppConversationStep.Idle, state.UserId, state.Role);
            return;
        }

        if (state.Step == WhatsAppConversationStep.AwaitingTicketId)
        {
            await SendTicketStatusAsync(
                message.FromPhoneNumber,
                state,
                text,
                cancellationToken);

            SetState(normalizedPhone, WhatsAppConversationStep.Idle, state.UserId, state.Role);
            return;
        }

        if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.Idle);
            await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
            return;
        }

        if (IsServiceAction(text))
        {
            await HandleServiceActionAsync(message, normalizedPhone, text, cancellationToken);
            return;
        }

        SetState(normalizedPhone, WhatsAppConversationStep.Idle);
        await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
    }

    private async Task HandleServiceActionAsync(
        WhatsAppIncomingMessage message,
        string normalizedPhone,
        string text,
        CancellationToken cancellationToken)
    {
        var user = await _userResolver.ResolveByWhatsAppPhoneAsync(
            message.FromPhoneNumber,
            cancellationToken);

        if (user == null)
        {
            await _sender.SendRegistrationRequiredMessageAsync(
                message.FromPhoneNumber,
                cancellationToken);

            return;
        }

        SetState(normalizedPhone, WhatsAppConversationStep.Idle, user.Id, user.Role);

        switch (text.ToLowerInvariant())
        {
            case "create_ticket":
                SetState(normalizedPhone, WhatsAppConversationStep.AwaitingNewTicketDescription, user.Id, user.Role);
                await _sender.SendTextMessageAsync(
                    message.FromPhoneNumber,
                    "Create Ticket\n\nPlease describe the issue you need help with.",
                    cancellationToken);
                break;

            case "check_status":
                SetState(normalizedPhone, WhatsAppConversationStep.AwaitingTicketId, user.Id, user.Role);
                await _sender.SendTextMessageAsync(
                    message.FromPhoneNumber,
                    "Please enter your ticket number, for example TKT-12345678-1234.",
                    cancellationToken);
                break;

            case "my_tickets":
                await SendRecentTicketsAsync(message.FromPhoneNumber, user, cancellationToken);
                break;

            case "download_report":
                await _sender.SendTextMessageAsync(
                    message.FromPhoneNumber,
                    "Please open your customer dashboard to download reports. WhatsApp report download will be added next.",
                    cancellationToken);
                break;

            case "talk_to_agent":
                SetState(normalizedPhone, WhatsAppConversationStep.AwaitingAgentIssue, user.Id, user.Role);
                await _sender.SendTextMessageAsync(
                    message.FromPhoneNumber,
                    "Talk to an Agent\n\nPlease describe your issue briefly. An agent will see your message.",
                    cancellationToken);
                break;
        }
    }

    private async Task HandleAwaitingAgentIssueAsync(
        WhatsAppIncomingMessage message,
        string normalizedPhone,
        string text,
        CancellationToken cancellationToken)
    {
        var state = GetState(normalizedPhone);

        SetState(
            normalizedPhone,
            WhatsAppConversationStep.HumanHandoff,
            state.UserId,
            state.Role);

        var conversation = await _conversationStore.GetOrCreateConversationAsync(
            message.FromPhoneNumber,
            message.CustomerName,
            cancellationToken);

        var savedMessage = await _conversationStore.AddInboundMessageAsync(
            conversation.Id,
            text,
            message.MessageId,
            cancellationToken);

        await BroadcastInboundMessageAsync(
            conversation.Id,
            savedMessage.Id,
            message.FromPhoneNumber,
            message.CustomerName,
            text,
            savedMessage.CreatedAt,
            cancellationToken);

        await _sender.SendTextMessageAsync(
            message.FromPhoneNumber,
            "Your message has been sent to our support team. You can continue typing here and an agent will respond shortly. Type \"end chat\" to return to the menu.",
            cancellationToken);
    }

    private async Task HandleHumanHandoffAsync(
        WhatsAppIncomingMessage message,
        string normalizedPhone,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
        {
            SetState(normalizedPhone, WhatsAppConversationStep.Idle);
            await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
            return;
        }

        var conversation = await _conversationStore.GetOrCreateConversationAsync(
            message.FromPhoneNumber,
            message.CustomerName,
            cancellationToken);

        var savedMessage = await _conversationStore.AddInboundMessageAsync(
            conversation.Id,
            text,
            message.MessageId,
            cancellationToken);

        await BroadcastInboundMessageAsync(
            conversation.Id,
            savedMessage.Id,
            message.FromPhoneNumber,
            message.CustomerName,
            text,
            savedMessage.CreatedAt,
            cancellationToken);

        await _sender.SendTextMessageAsync(
            message.FromPhoneNumber,
            "Your message has been sent to the support team. Type \"end chat\" to return to the menu.",
            cancellationToken);
    }

    private async Task BroadcastInboundMessageAsync(
        Guid conversationId,
        Guid messageId,
        string fromPhoneNumber,
        string customerName,
        string text,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var liveChatMessage = new WhatsAppLiveChatMessage(
            conversationId,
            messageId,
            fromPhoneNumber,
            customerName,
            text,
            "Inbound",
            createdAt);

        var conversationUpdated = new WhatsAppConversationUpdated(
            conversationId,
            fromPhoneNumber,
            customerName,
            text,
            "Inbound",
            createdAt);

        await _liveChatHub.Clients.All.SendAsync(
            "ReceiveConversationUpdated",
            conversationUpdated,
            cancellationToken);

        await _liveChatHub.Clients
            .Group(WhatsAppLiveChatHub.GetConversationGroupName(conversationId))
            .SendAsync(
                "ReceiveWhatsAppMessage",
                liveChatMessage,
                cancellationToken);
    }

    private async Task CreateTicketFromWhatsAppAsync(
        WhatsAppIncomingMessage message,
        string normalizedPhone,
        string description,
        CancellationToken cancellationToken)
    {
        var state = GetState(normalizedPhone);
        Users? user = null;

        if (state.UserId.HasValue)
        {
            user = await _context.Users.FirstOrDefaultAsync(
                u => u.Id == state.UserId.Value && u.IsActive,
                cancellationToken);
        }

        user ??= await _userResolver.ResolveByWhatsAppPhoneAsync(
            message.FromPhoneNumber,
            cancellationToken);

        if (user == null)
        {
            await _sender.SendRegistrationRequiredMessageAsync(
                message.FromPhoneNumber,
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
        WhatsAppConversationState state,
        string ticketNumber,
        CancellationToken cancellationToken)
    {
        Users? user = null;

        if (state.UserId.HasValue)
        {
            user = await _context.Users.FirstOrDefaultAsync(
                u => u.Id == state.UserId.Value && u.IsActive,
                cancellationToken);
        }

        user ??= await _userResolver.ResolveByWhatsAppPhoneAsync(
            whatsappPhone,
            cancellationToken);

        if (user == null)
        {
            await _sender.SendRegistrationRequiredMessageAsync(whatsappPhone, cancellationToken);
            return;
        }

        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t =>
                t.TicketNumber == ticketNumber.Trim() &&
                t.CreatedByUserId == user.Id,
                cancellationToken);

        if (ticket == null)
        {
            await _sender.SendTextMessageAsync(
                whatsappPhone,
                "Ticket not found for your account. Please check the ticket number and try again.",
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
        Users user,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.GetMyTicketsAsync(user.Id, 1, 5, user.Role);

        if (result.Items.Count == 0)
        {
            await _sender.SendTextMessageAsync(
                whatsappPhone,
                "No recent tickets were found for your account.",
                cancellationToken);

            return;
        }

        var lines = result.Items.Select(t =>
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

    private void SetState(
        string phoneNumber,
        WhatsAppConversationStep step,
        Guid? userId = null,
        UserRole? role = null)
    {
        var current = GetState(phoneNumber);
        var key = GetCacheKey(phoneNumber);

        _cache.Set(
            key,
            new WhatsAppConversationState
            {
                Step = step,
                UserId = userId ?? current.UserId,
                Role = role ?? current.Role,
                UpdatedAtUtc = DateTime.UtcNow
            },
            TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes));
    }

    private static string GetCacheKey(string phoneNumber)
        => $"whatsapp:conversation:{phoneNumber}";

    private static bool IsServiceAction(string text)
    {
        return text.Equals("create_ticket", StringComparison.OrdinalIgnoreCase)
            || text.Equals("check_status", StringComparison.OrdinalIgnoreCase)
            || text.Equals("my_tickets", StringComparison.OrdinalIgnoreCase)
            || text.Equals("download_report", StringComparison.OrdinalIgnoreCase)
            || text.Equals("talk_to_agent", StringComparison.OrdinalIgnoreCase);
    }
}



// using System.Text.RegularExpressions;
// using Microsoft.AspNetCore.SignalR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Caching.Memory;
// using Microsoft.Extensions.Options;
// using TicketingSystem.Api.Data;
// using TicketingSystem.Api.Dtos.Tickets;
// using TicketingSystem.Api.DTOs.Tickets;
// using TicketingSystem.Api.Hubs;
// using TicketingSystem.Api.Models;

// namespace TicketingSystem.Api.Services;

// public interface IWhatsAppConversationService
// {
//     Task HandleIncomingMessageAsync(
//         WhatsAppIncomingMessage message,
//         CancellationToken cancellationToken = default);
// }

// public sealed class WhatsAppConversationService : IWhatsAppConversationService
// {
//     private readonly IMemoryCache _cache;
//     private readonly AppDbContext _context;
//     private readonly ITicketService _ticketService;
//     private readonly IWhatsAppSenderService _sender;
//     private readonly IWhatsAppUserResolver _userResolver;
//     private readonly IPhoneNumberService _phoneNumberService;
//     private readonly IWhatsAppConversationStore _conversationStore;
//     private readonly WhatsAppOptions _options;
//     private readonly ILogger<WhatsAppConversationService> _logger;
//     private readonly IHubContext<WhatsAppLiveChatHub> _liveChatHub; // signalR hub context for live chat

//     public WhatsAppConversationService(
//         IMemoryCache cache,
//         AppDbContext context,
//         ITicketService ticketService,
//         IWhatsAppSenderService sender,
//         IWhatsAppUserResolver userResolver,
//         IPhoneNumberService phoneNumberService,
//         IWhatsAppConversationStore conversationStore,
//         IOptions<WhatsAppOptions> options,
//         ILogger<WhatsAppConversationService> logger,
//         IHubContext<WhatsAppLiveChatHub> liveChatHub) 
//     {
//         _cache = cache;
//         _context = context;
//         _ticketService = ticketService;
//         _sender = sender;
//         _userResolver = userResolver;
//         _phoneNumberService = phoneNumberService;
//         _conversationStore = conversationStore;
//         _options = options.Value;
//         _logger = logger;
//         _liveChatHub = liveChatHub;
//     }

//     public async Task HandleIncomingMessageAsync(
//         WhatsAppIncomingMessage message,
//         CancellationToken cancellationToken = default)
//     {
//         var text = message.Text.Trim();
//         var normalizedPhone = _phoneNumberService.NormalizeKenyanMobile(message.FromPhoneNumber);

//         if (string.IsNullOrWhiteSpace(normalizedPhone))
//         {
//             await _sender.SendTextMessageAsync(
//                 message.FromPhoneNumber,
//                 "Sorry, we could not read your phone number correctly.",
//                 cancellationToken);

//             return;
//         }

//         var state = GetState(normalizedPhone);

//         if (state.Step == WhatsAppConversationStep.HumanHandoff)
//         {
//             await HandleHumanHandoffAsync(message, normalizedPhone, text, cancellationToken);
//             return;
//         }

//         if (state.Step == WhatsAppConversationStep.AwaitingAgentIssue)
//         {
//             await HandleAwaitingAgentIssueAsync(message, normalizedPhone, text, cancellationToken);
//             return;
//         }

//         if (state.Step == WhatsAppConversationStep.AwaitingNewTicketDescription)
//         {
//             await CreateTicketFromWhatsAppAsync(message, normalizedPhone, text, cancellationToken);
//             SetState(normalizedPhone, WhatsAppConversationStep.Idle, state.UserId, state.Role);
//             return;
//         }

//         if (state.Step == WhatsAppConversationStep.AwaitingTicketId)
//         {
//             await SendTicketStatusAsync(
//                 message.FromPhoneNumber,
//                 state,
//                 text,
//                 cancellationToken);

//             SetState(normalizedPhone, WhatsAppConversationStep.Idle, state.UserId, state.Role);
//             return;
//         }

//         if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
//         {
//             SetState(normalizedPhone, WhatsAppConversationStep.Idle);
//             await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
//             return;
//         }

//         if (IsServiceAction(text))
//         {
//             await HandleServiceActionAsync(message, normalizedPhone, text, cancellationToken);
//             return;
//         }

//         SetState(normalizedPhone, WhatsAppConversationStep.Idle);
//         await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
//     }

//     private async Task HandleServiceActionAsync(
//         WhatsAppIncomingMessage message,
//         string normalizedPhone,
//         string text,
//         CancellationToken cancellationToken)
//     {
//         var user = await _userResolver.ResolveByWhatsAppPhoneAsync(message.FromPhoneNumber, cancellationToken);

//         if (user == null)
//         {
//             await _sender.SendRegistrationRequiredMessageAsync(message.FromPhoneNumber, cancellationToken);
//             return;
//         }

//         SetState(normalizedPhone, WhatsAppConversationStep.Idle, user.Id, user.Role);

//         switch (text.ToLowerInvariant())
//         {
//             case "create_ticket":
//                 SetState(normalizedPhone, WhatsAppConversationStep.AwaitingNewTicketDescription, user.Id, user.Role);
//                 await _sender.SendTextMessageAsync(
//                     message.FromPhoneNumber,
//                     "Create Ticket\n\nPlease describe the issue you need help with.",
//                     cancellationToken);
//                 break;

//             case "check_status":
//                 SetState(normalizedPhone, WhatsAppConversationStep.AwaitingTicketId, user.Id, user.Role);
//                 await _sender.SendTextMessageAsync(
//                     message.FromPhoneNumber,
//                     "Please enter your ticket number, for example TKT-12345678-1234.",
//                     cancellationToken);
//                 break;

//             case "my_tickets":
//                 await SendRecentTicketsAsync(message.FromPhoneNumber, user, cancellationToken);
//                 break;

//             case "download_report":
//                 await _sender.SendTextMessageAsync(
//                     message.FromPhoneNumber,
//                     "Please open your customer dashboard to download reports. WhatsApp report download will be added next.",
//                     cancellationToken);
//                 break;

//             case "talk_to_agent":
//                 SetState(normalizedPhone, WhatsAppConversationStep.AwaitingAgentIssue, user.Id, user.Role);
//                 await _sender.SendTextMessageAsync(
//                     message.FromPhoneNumber,
//                     "Talk to an Agent\n\nPlease describe your issue briefly. An agent will see your message.",
//                     cancellationToken);
//                 break;
//         }
//     }

//     private async Task HandleAwaitingAgentIssueAsync(
//         WhatsAppIncomingMessage message,
//         string normalizedPhone,
//         string text,
//         CancellationToken cancellationToken)
//     {
//         SetState(
//             normalizedPhone,
//             WhatsAppConversationStep.HumanHandoff,
//             GetState(normalizedPhone).UserId,
//             GetState(normalizedPhone).Role);

//         var conversation = await _conversationStore.GetOrCreateConversationAsync(
//             message.FromPhoneNumber,
//             message.CustomerName,
//             cancellationToken);

//         var savedMessage = await _conversationStore.AddInboundMessageAsync(
//             conversation.Id,
//             text,
//             message.MessageId,
//             cancellationToken);

//         var liveChatMessage = new WhatsAppLiveChatMessage(
//             conversation.Id,
//             savedMessage.Id,
//             message.FromPhoneNumber,
//             message.CustomerName,
//             text,
//             "Inbound",
//             savedMessage.CreatedAt);

//         await _liveChatHub.Clients
//             .Group(WhatsAppLiveChatHub.GetConversationGroupName(conversation.Id))
//             .SendAsync(
//                 "ReceiveWhatsAppMessage",
//                 liveChatMessage,
//                 cancellationToken);

//         await _sender.SendTextMessageAsync(
//             message.FromPhoneNumber,
//             "Your message has been sent to our support team. You can continue typing here and an agent will respond shortly. Type \"end chat\" to return to the menu.",
//             cancellationToken);
//     }

//     private async Task HandleHumanHandoffAsync(
//         WhatsAppIncomingMessage message,
//         string normalizedPhone,
//         string text,
//         CancellationToken cancellationToken)
//     {
//         if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
//         {
//             SetState(normalizedPhone, WhatsAppConversationStep.Idle);
//             await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
//             return;
//         }

//         var conversation = await _conversationStore.GetOrCreateConversationAsync(
//             message.FromPhoneNumber,
//             message.CustomerName,
//             cancellationToken);

//         var savedMessage = await _conversationStore.AddInboundMessageAsync(
//             conversation.Id,
//             text,
//             message.MessageId,
//             cancellationToken);

//         var liveChatMessage = new WhatsAppLiveChatMessage(
//             conversation.Id,
//             savedMessage.Id,
//             message.FromPhoneNumber,
//             message.CustomerName,
//             text,
//             "Inbound",
//             savedMessage.CreatedAt);

//         await _liveChatHub.Clients
//             .Group(WhatsAppLiveChatHub.GetConversationGroupName(conversation.Id))
//             .SendAsync(
//                 "ReceiveWhatsAppMessage",
//                 liveChatMessage,
//                 cancellationToken);

//         await _sender.SendTextMessageAsync(
//             message.FromPhoneNumber,
//             "Your message has been sent to the support team. Type \"end chat\" to return to the menu.",
//             cancellationToken);
//     }

//     // private async Task HandleAwaitingAgentIssueAsync(
//     //     WhatsAppIncomingMessage message,
//     //     string normalizedPhone,
//     //     string text,
//     //     CancellationToken cancellationToken)
//     // {
//     //     SetState(normalizedPhone, WhatsAppConversationStep.HumanHandoff, GetState(normalizedPhone).UserId, GetState(normalizedPhone).Role);

//     //     var liveChatMessage = new WhatsAppLiveChatMessage(
//     //         message.FromPhoneNumber,
//     //         message.CustomerName,
//     //         text,
//     //         DateTime.UtcNow);

//     //     await _liveChatHub.Clients.All.SendAsync(
//     //         "ReceiveWhatsAppMessage",
//     //         liveChatMessage,
//     //         cancellationToken);

//     //     await _sender.SendTextMessageAsync(
//     //         message.FromPhoneNumber,
//     //         "Your message has been sent to our support team. You can continue typing here and an agent will respond shortly. Type \"end chat\" to return to the menu.",
//     //         cancellationToken);
//     // }

//     // private async Task HandleHumanHandoffAsync(
//     //     WhatsAppIncomingMessage message,
//     //     string normalizedPhone,
//     //     string text,
//     //     CancellationToken cancellationToken)
//     // {
//     //     if (text.Equals("end chat", StringComparison.OrdinalIgnoreCase))
//     //     {
//     //         SetState(normalizedPhone, WhatsAppConversationStep.Idle);
//     //         await _sender.SendWelcomeAndMenuAsync(message.FromPhoneNumber, cancellationToken);
//     //         return;
//     //     }

//     //     var liveChatMessage = new WhatsAppLiveChatMessage(
//     //         message.FromPhoneNumber,
//     //         message.CustomerName,
//     //         text,
//     //         DateTime.UtcNow);

//     //     await _liveChatHub.Clients.All.SendAsync(
//     //         "ReceiveWhatsAppMessage",
//     //         liveChatMessage,
//     //         cancellationToken);

//     //     await _sender.SendTextMessageAsync(
//     //         message.FromPhoneNumber,
//     //         "Your message has been sent to the support team. Type \"end chat\" to return to the menu.",
//     //         cancellationToken);
//     // }

//     private async Task CreateTicketFromWhatsAppAsync(
//         WhatsAppIncomingMessage message,
//         string normalizedPhone,
//         string description,
//         CancellationToken cancellationToken)
//     {
//         var state = GetState(normalizedPhone);
//         Users? user = null;

//         if (state.UserId.HasValue)
//         {
//             user = await _context.Users.FirstOrDefaultAsync(
//                 u => u.Id == state.UserId.Value && u.IsActive,
//                 cancellationToken);
//         }

//         user ??= await _userResolver.ResolveByWhatsAppPhoneAsync(message.FromPhoneNumber, cancellationToken);

//         if (user == null)
//         {
//             await _sender.SendRegistrationRequiredMessageAsync(message.FromPhoneNumber, cancellationToken);
//             return;
//         }

//         var dto = new CreateTicketDto
//         {
//             Title = $"WhatsApp request from {message.CustomerName}",
//             Description = description,
//             Category = "WhatsApp",
//             Priority = Priority.Medium
//         };

//         var result = await _ticketService.CreateTicketAsync(dto, user.Id);

//         await _sender.SendTextMessageAsync(
//             message.FromPhoneNumber,
//             $"Ticket Created\n\nYour ticket {result.TicketNumber} has been created. You will receive updates here on WhatsApp.",
//             cancellationToken);
//     }

//     private async Task SendTicketStatusAsync(
//         string whatsappPhone,
//         WhatsAppConversationState state,
//         string ticketNumber,
//         CancellationToken cancellationToken)
//     {
//         Users? user = null;

//         if (state.UserId.HasValue)
//         {
//             user = await _context.Users.FirstOrDefaultAsync(
//                 u => u.Id == state.UserId.Value && u.IsActive,
//                 cancellationToken);
//         }

//         user ??= await _userResolver.ResolveByWhatsAppPhoneAsync(whatsappPhone, cancellationToken);

//         if (user == null)
//         {
//             await _sender.SendRegistrationRequiredMessageAsync(whatsappPhone, cancellationToken);
//             return;
//         }

//         var ticket = await _context.Tickets
//             .FirstOrDefaultAsync(t =>
//                 t.TicketNumber == ticketNumber.Trim() && t.CreatedByUserId == user.Id,
//                 cancellationToken);

//         if (ticket == null)
//         {
//             await _sender.SendTextMessageAsync(
//                 whatsappPhone,
//                 "Ticket not found for your account. Please check the ticket number and try again.",
//                 cancellationToken);

//             return;
//         }

//         await _sender.SendTextMessageAsync(
//             whatsappPhone,
//             $"Ticket Status\n\nTicket: {ticket.TicketNumber}\nTitle: {ticket.Title}\nStatus: {ticket.Status}\nPriority: {ticket.Priority}",
//             cancellationToken);
//     }

//     private async Task SendRecentTicketsAsync(
//         string whatsappPhone,
//         Users user,
//         CancellationToken cancellationToken)
//     {
//         var result = await _ticketService.GetMyTicketsAsync(user.Id, 1, 5, user.Role);

//         if (result.Items.Count == 0)
//         {
//             await _sender.SendTextMessageAsync(
//                 whatsappPhone,
//                 "No recent tickets were found for your account.",
//                 cancellationToken);

//             return;
//         }

//         var lines = result.Items.Select(t =>
//             $"{t.TicketNumber} - {t.Status} - {t.Title}");

//         await _sender.SendTextMessageAsync(
//             whatsappPhone,
//             "Your Recent Tickets\n\n" + string.Join("\n", lines),
//             cancellationToken);
//     }

//     private WhatsAppConversationState GetState(string phoneNumber)
//     {
//         var key = GetCacheKey(phoneNumber);

//         return _cache.GetOrCreate(key, entry =>
//         {
//             entry.AbsoluteExpirationRelativeToNow =
//                 TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes);

//             return new WhatsAppConversationState();
//         })!;
//     }

//     private void SetState(
//         string phoneNumber,
//         WhatsAppConversationStep step,
//         Guid? userId = null,
//         UserRole? role = null)
//     {
//         var current = GetState(phoneNumber);
//         var key = GetCacheKey(phoneNumber);

//         _cache.Set(
//             key,
//             new WhatsAppConversationState
//             {
//                 Step = step,
//                 UserId = userId ?? current.UserId,
//                 Role = role ?? current.Role,
//                 UpdatedAtUtc = DateTime.UtcNow
//             },
//             TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes));
//     }

//     private static string GetCacheKey(string phoneNumber)
//         => $"whatsapp:conversation:{phoneNumber}";

//     private static bool IsServiceAction(string text)
//     {
//         return text.Equals("create_ticket", StringComparison.OrdinalIgnoreCase)
//             || text.Equals("check_status", StringComparison.OrdinalIgnoreCase)
//             || text.Equals("my_tickets", StringComparison.OrdinalIgnoreCase)
//             || text.Equals("download_report", StringComparison.OrdinalIgnoreCase)
//             || text.Equals("talk_to_agent", StringComparison.OrdinalIgnoreCase);
//     }
// }
