using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.WhatsApp;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Support,TeamLead,SuperAdmin")]
[Route("api/whatsapp/conversations")]
public sealed class WhatsAppConversationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public WhatsAppConversationsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations(
        CancellationToken cancellationToken)
    {
        var conversations = await _context.Conversations
            .AsNoTracking()
            .Include(c => c.CustomerProfile)
            .Include(c => c.Messages)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new WhatsAppConversationListItemDto
            {
                ConversationId = c.Id,
                CustomerName = c.CustomerProfile.DisplayName,
                PhoneNumber = c.CustomerProfile.PhoneNumber,
                Status = c.Status.ToString(),
                LastMessageAt = c.LastMessageAt,
                LastMessagePreview = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Body.Length > 80
                        ? m.Body.Substring(0, 80) + "..."
                        : m.Body)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = conversations
        });
    }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversationExists = await _context.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId, cancellationToken);

        if (!conversationExists)
            return NotFound("Conversation not found.");

        var messages = await _context.ConversationMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new WhatsAppConversationMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                Direction = m.Direction.ToString(),
                SenderType = m.SenderType.ToString(),
                SenderUserId = m.SenderUserId,
                SenderName = m.SenderUser == null
                    ? null
                    : $"{m.SenderUser.FirstName} {m.SenderUser.LastName}",
                Body = m.Body,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = messages
        });
    }
}