using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface IWhatsAppConversationStore
{
    Task<Conversation> GetOrCreateConversationAsync(
        string whatsappPhoneNumber,
        string customerName,
        CancellationToken cancellationToken = default);

    Task<ConversationMessage> AddInboundMessageAsync(
        Guid conversationId,
        string body,
        string? externalMessageId,
        CancellationToken cancellationToken = default);

    Task<ConversationMessage> AddOutboundMessageAsync(
        Guid conversationId,
        string body,
        Guid? senderUserId,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppConversationStore : IWhatsAppConversationStore
{
    private readonly AppDbContext _context;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IWhatsAppUserResolver _userResolver;

    public WhatsAppConversationStore(
        AppDbContext context,
        IPhoneNumberService phoneNumberService,
        IWhatsAppUserResolver userResolver)
    {
        _context = context;
        _phoneNumberService = phoneNumberService;
        _userResolver = userResolver;
    }

    public async Task<Conversation> GetOrCreateConversationAsync(
        string whatsappPhoneNumber,
        string customerName,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = _phoneNumberService.NormalizeKenyanMobile(whatsappPhoneNumber);

        if (string.IsNullOrWhiteSpace(normalizedPhone))
            throw new InvalidOperationException("WhatsApp phone number is missing or invalid.");

        var existingIdentity = await _context.ChannelIdentities
            .Include(i => i.Conversation)
                .ThenInclude(c => c.CustomerProfile)
            .FirstOrDefaultAsync(
                i => i.Channel == ConversationChannel.WhatsApp &&
                     i.ExternalId == normalizedPhone,
                cancellationToken);

        if (existingIdentity != null)
            return existingIdentity.Conversation;

        var linkedUser = await _userResolver.ResolveByWhatsAppPhoneAsync(
            whatsappPhoneNumber,
            cancellationToken);

        var customerProfile = new CustomerProfile
        {
            UserId = linkedUser?.Id,
            DisplayName = string.IsNullOrWhiteSpace(customerName)
                ? linkedUser is null
                    ? "WhatsApp Customer"
                    : $"{linkedUser.FirstName} {linkedUser.LastName}"
                : customerName,
            PhoneNumber = normalizedPhone
        };

        var conversation = new Conversation
        {
            CustomerProfile = customerProfile,
            Channel = ConversationChannel.WhatsApp,
            Status = ConversationStatus.WaitingForAgent,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        var channelIdentity = new ChannelIdentity
        {
            CustomerProfile = customerProfile,
            Conversation = conversation,
            Channel = ConversationChannel.WhatsApp,
            ExternalId = normalizedPhone,
            CreatedAt = DateTime.UtcNow
        };

        _context.CustomerProfiles.Add(customerProfile);
        _context.Conversations.Add(conversation);
        _context.ChannelIdentities.Add(channelIdentity);

        await _context.SaveChangesAsync(cancellationToken);

        return conversation;
    }

    public async Task<ConversationMessage> AddInboundMessageAsync(
        Guid conversationId,
        string body,
        string? externalMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            Direction = MessageDirection.Inbound,
            SenderType = MessageSenderType.Customer,
            Body = body,
            ExternalMessageId = externalMessageId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ConversationMessages.Add(message);

        await TouchConversationAsync(conversationId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return message;
    }

    public async Task<ConversationMessage> AddOutboundMessageAsync(
        Guid conversationId,
        string body,
        Guid? senderUserId,
        CancellationToken cancellationToken = default)
    {
        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            Direction = MessageDirection.Outbound,
            SenderType = senderUserId.HasValue
                ? MessageSenderType.Agent
                : MessageSenderType.Bot,
            SenderUserId = senderUserId,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        _context.ConversationMessages.Add(message);

        await TouchConversationAsync(conversationId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return message;
    }

    private async Task TouchConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation == null)
            throw new InvalidOperationException("Conversation was not found.");

        conversation.LastMessageAt = DateTime.UtcNow;

        if (conversation.Status == ConversationStatus.Closed)
            conversation.Status = ConversationStatus.Open;
    }
}