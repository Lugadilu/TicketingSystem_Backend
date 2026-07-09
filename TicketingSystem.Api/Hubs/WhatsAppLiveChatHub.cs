using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TicketingSystem.Api.Hubs;

[Authorize(Roles = "Support,TeamLead,SuperAdmin")]
public sealed class WhatsAppLiveChatHub : Hub
{
    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetConversationGroupName(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetConversationGroupName(conversationId));
    }

    public static string GetConversationGroupName(Guid conversationId)
        => $"conversation-{conversationId}";
}




// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.SignalR;

// namespace TicketingSystem.Api.Hubs;

// [Authorize(Roles = "Support,TeamLead,SuperAdmin")]
// public sealed class WhatsAppLiveChatHub : Hub
// {
// }