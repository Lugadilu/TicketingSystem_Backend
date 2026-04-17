using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Comments;
using TicketingSystem.Api.Dtos.Common;
using TicketingSystem.Api.DTOs.ActivityLogs;
using TicketingSystem.Api.DTOs.Tickets;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface ITicketService
{
    Task<CreateTicketResponseDto> CreateTicketAsync(CreateTicketDto dto, Guid createdByUserId);
    Task<TicketResponseDto> GetTicketByIdAsync(Guid ticketId);
    Task<TicketResponseDto> GetTicketByIdAsync(Guid ticketId, Guid userId, UserRole userRole); // ✅ NEW OVERLOAD
    Task<PaginatedResponseDto<TicketListItemDto>> GetAllTicketsAsync(int pageNumber = 1, int pageSize = 10);
    Task<PaginatedResponseDto<TicketListItemDto>> GetTicketsByStatusAsync(TicketStatus status, int pageNumber = 1, int pageSize = 10);
    Task<PaginatedResponseDto<TicketListItemDto>> GetMyTicketsAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<PaginatedResponseDto<TicketListItemDto>> GetMyTicketsAsync(Guid userId, int pageNumber, int pageSize, UserRole userRole); // ✅ NEW OVERLOAD
    Task<PaginatedResponseDto<TicketListItemDto>> GetAssignedToMeAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<TicketResponseDto> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto);
    Task<TicketResponseDto> AssignTicketAsync(Guid ticketId, AssignTicketDto dto, Guid assignedByUserId);
    Task<TicketResponseDto> ChangeStatusAsync(Guid ticketId, ChangeTicketStatusDto dto);
    Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId);
    Task<CommentResponseDto> AddCommentAsync(Guid ticketId, CreateCommentDto dto, Guid userId);
    Task<List<CommentResponseDto>> GetCommentsAsync(Guid ticketId);
    Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);
    Task<List<ActivityLogResponseDto>> GetTicketActivityAsync(Guid ticketId);
}

public class TicketService : ITicketService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public TicketService(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<CreateTicketResponseDto> CreateTicketAsync(CreateTicketDto dto, Guid createdByUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == createdByUserId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        var ticketNumber = GenerateTicketNumber();

        var ticket = new Tickets
        {
            Id = Guid.NewGuid(),
            TicketNumber = ticketNumber,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Priority = dto.Priority,
            Status = TicketStatus.Open,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticket.Id, createdByUserId, "Ticket Created", null, null);

        //  SEND EMAIL NOTIFICATION - TICKET CREATION
        try
        {
            var emailSubject = $"Ticket Created: {ticket.TicketNumber}";
            var emailBody = $@"
            Dear {user.FirstName},

            Your ticket has been created successfully.

            Ticket Details:
            - Ticket Number: {ticket.TicketNumber}
            - Title: {ticket.Title}
            - Description: {ticket.Description}
            - Category: {ticket.Category}
            - Priority: {(Priority)ticket.Priority}
            - Status: {ticket.Status}
            - Created Date: {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}

            You can track your ticket using the ticket number above.

            Best regards,
            Ticketing System
            ";
            await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send ticket creation email: {ex.Message}");
        }

        return new CreateTicketResponseDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Message = $"Ticket {ticket.TicketNumber} created successfully"
        };
    }

    // ============================================================
    // OLD METHOD: GetTicketByIdAsync (Guid ticketId)
    // ============================================================
    public async Task<TicketResponseDto> GetTicketByIdAsync(Guid ticketId)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .Include(t => t.Comments)
            .Include(t => t.ActivityLogs)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        return MapToTicketResponseDto(ticket);
    }

    // ============================================================
    // NEW OVERLOAD: GetTicketByIdAsync with role-based access control
    // ============================================================
    /// <summary>
    /// Get ticket by ID with role-based access control
    /// - Customer: can only see tickets they created
    /// - Technician: can only see tickets assigned to them
    /// - TeamLead: can see tickets they created or assigned to their team
    /// - SuperAdmin: can see all tickets
    /// - Support: can see all tickets
    /// </summary>
    public async Task<TicketResponseDto> GetTicketByIdAsync(Guid ticketId, Guid userId, UserRole userRole)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .Include(t => t.Comments)
            .Include(t => t.ActivityLogs)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        // ===== ROLE-BASED ACCESS CONTROL =====
        var hasAccess = userRole switch
        {
            // Customer: only own tickets
            UserRole.Customer => ticket.CreatedByUserId == userId,

            // Technician: only assigned tickets
            UserRole.Technician => ticket.AssignedToUserId == userId,

            // TeamLead: own tickets + team's tickets
            UserRole.TeamLead =>
                ticket.CreatedByUserId == userId ||
                (ticket.AssignedToUserId.HasValue &&
                 _context.Users.Any(u => u.Id == ticket.AssignedToUserId && u.TeamLeadUserId == userId)),

            // SuperAdmin: all tickets
            UserRole.SuperAdmin => true,

            // Support: all tickets
            UserRole.Support => true,

            // Default: no access
            _ => false
        };

        if (!hasAccess)
            throw new InvalidOperationException("You don't have permission to view this ticket");

        return MapToTicketResponseDto(ticket);
    }

    public async Task<PaginatedResponseDto<TicketListItemDto>> GetAllTicketsAsync(int pageNumber = 1, int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var tickets = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedByUserName = $"{t.CreatedByUser.FirstName} {t.CreatedByUser.LastName}",
                AssignedToUserName = t.AssignedToUser != null ? $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}" : "Unassigned",
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketListItemDto>
        {
            Items = tickets,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponseDto<TicketListItemDto>> GetTicketsByStatusAsync(TicketStatus status, int pageNumber = 1, int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var tickets = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedByUserName = $"{t.CreatedByUser.FirstName} {t.CreatedByUser.LastName}",
                AssignedToUserName = t.AssignedToUser != null ? $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}" : "Unassigned",
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketListItemDto>
        {
            Items = tickets,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    // ============================================================
    // OLD METHOD: GetMyTicketsAsync (Guid userId, int pageNumber, int pageSize)
    // ============================================================
    public async Task<PaginatedResponseDto<TicketListItemDto>> GetMyTicketsAsync(Guid userId, int pageNumber = 1, int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var tickets = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedByUserName = $"{t.CreatedByUser.FirstName} {t.CreatedByUser.LastName}",
                AssignedToUserName = t.AssignedToUser != null ? $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}" : "Unassigned",
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketListItemDto>
        {
            Items = tickets,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    // ============================================================
    // NEW OVERLOAD: GetMyTicketsAsync with role-based filtering
    // ============================================================
    /// <summary>
    /// Get tickets based on user role
    /// - Customer: sees only tickets they created
    /// - Technician: sees only tickets assigned to them (not closed)
    /// - TeamLead: sees tickets they created + tickets assigned to their team
    /// - SuperAdmin: sees all tickets
    /// - Support: sees all tickets
    /// </summary>
    public async Task<PaginatedResponseDto<TicketListItemDto>> GetMyTicketsAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 10,
        UserRole userRole = UserRole.Customer)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        IQueryable<Tickets> query;

        // ===== ROLE-BASED FILTERING =====
        switch (userRole)
        {
            // CUSTOMER: Only sees tickets they created
            case UserRole.Customer:
                query = _context.Tickets
                    .Where(t => t.CreatedByUserId == userId)
                    .OrderByDescending(t => t.CreatedAt);
                break;

            // TECHNICIAN: Only sees tickets assigned to them (excluding closed)
            case UserRole.Technician:
                query = _context.Tickets
                    .Where(t => t.AssignedToUserId == userId && t.Status != TicketStatus.Closed)
                    .OrderByDescending(t => t.CreatedAt);
                break;

            // TEAMLEAD: Sees tickets created by them + assigned to their technicians
            case UserRole.TeamLead:
                var teamMemberIds = await _context.Users
                    .Where(u => u.TeamLeadUserId == userId && u.Role == UserRole.Technician)
                    .Select(u => u.Id)
                    .ToListAsync();

                query = _context.Tickets
                    .Where(t =>
                        t.CreatedByUserId == userId ||  // Tickets they created
                        teamMemberIds.Contains(t.AssignedToUserId ?? Guid.Empty)  // Tickets for their team
                    )
                    .OrderByDescending(t => t.CreatedAt);
                break;

            // SUPERADMIN: Sees all tickets
            case UserRole.SuperAdmin:
                query = _context.Tickets
                    .OrderByDescending(t => t.CreatedAt);
                break;

            // SUPPORT: Sees all tickets (for future use)
            case UserRole.Support:
                query = _context.Tickets
                    .OrderByDescending(t => t.CreatedAt);
                break;

            // DEFAULT: No access
            default:
                query = _context.Tickets.Where(t => false);  // Empty result
                break;
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and get results
        var tickets = await query
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedByUserName = $"{t.CreatedByUser.FirstName} {t.CreatedByUser.LastName}",
                AssignedToUserName = t.AssignedToUser != null
                    ? $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}"
                    : "Unassigned",
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketListItemDto>
        {
            Items = tickets,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponseDto<TicketListItemDto>> GetAssignedToMeAsync(Guid userId, int pageNumber = 1, int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.AssignedToUserId == userId && t.Status != TicketStatus.Closed)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var tickets = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedByUserName = $"{t.CreatedByUser.FirstName} {t.CreatedByUser.LastName}",
                AssignedToUserName = t.AssignedToUser != null ? $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}" : "Unassigned",
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketListItemDto>
        {
            Items = tickets,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TicketResponseDto> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var oldTitle = ticket.Title;
        var oldDescription = ticket.Description;
        var oldCategory = ticket.Category;
        var oldPriority = ticket.Priority.ToString();

        if (!string.IsNullOrEmpty(dto.Title))
            ticket.Title = dto.Title;

        if (!string.IsNullOrEmpty(dto.Description))
            ticket.Description = dto.Description;

        if (!string.IsNullOrEmpty(dto.Category))
            ticket.Category = dto.Category;

        if (dto.Priority.HasValue)
            ticket.Priority = dto.Priority.Value;

        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        if (ticket.Title != oldTitle)
            await LogActivityAsync(ticketId, ticket.CreatedByUserId, "Title Updated", oldTitle, ticket.Title);

        if (ticket.Description != oldDescription)
            await LogActivityAsync(ticketId, ticket.CreatedByUserId, "Description Updated", oldDescription, ticket.Description);

        if (ticket.Category != oldCategory)
            await LogActivityAsync(ticketId, ticket.CreatedByUserId, "Category Updated", oldCategory, ticket.Category);

        if (ticket.Priority.ToString() != oldPriority)
            await LogActivityAsync(ticketId, ticket.CreatedByUserId, "Priority Updated", oldPriority, ticket.Priority.ToString());

        return MapToTicketResponseDto(ticket);
    }

    public async Task<TicketResponseDto> AssignTicketAsync(Guid ticketId, AssignTicketDto dto, Guid assignedByUserId)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var technician = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.AssignedToUserId);
        if (technician == null)
            throw new InvalidOperationException("Technician not found");

        var oldAssignedTo = ticket.AssignedToUser != null ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}" : "Unassigned";
        var newAssignedTo = $"{technician.FirstName} {technician.LastName}";

        ticket.AssignedToUserId = dto.AssignedToUserId;
        ticket.AssignedByUserId = assignedByUserId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, assignedByUserId, "Ticket Assigned", oldAssignedTo, newAssignedTo);

        // ✅ SEND EMAIL NOTIFICATION - TICKET ASSIGNMENT
        try
        {
            var emailSubject = $"Ticket Assigned to You: {ticket.TicketNumber}";
            var emailBody = $@"
Dear {technician.FirstName},

A ticket has been assigned to you.

Ticket Details:
- Ticket Number: {ticket.TicketNumber}
- Title: {ticket.Title}
- Description: {ticket.Description}
- Category: {ticket.Category}
- Priority: {(Priority)ticket.Priority}
- Status: {ticket.Status}
- Created By: {ticket.CreatedByUser.FirstName} {ticket.CreatedByUser.LastName}
- Assigned Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}

Please review and take action on this ticket.

Best regards,
Ticketing System
";
            await _emailService.SendEmailAsync(technician.Email, emailSubject, emailBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send ticket assignment email: {ex.Message}");
        }

        return MapToTicketResponseDto(ticket);
    }

    public async Task<TicketResponseDto> ChangeStatusAsync(Guid ticketId, ChangeTicketStatusDto dto)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var oldStatus = ticket.Status.ToString();
        ticket.Status = dto.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        // IMPORTANT: Pass null for changedByUserId (no specific user for system action)
        await LogActivityAsync(ticketId, null, "Status Changed", oldStatus, dto.Status.ToString());

        // ✅ SEND EMAIL NOTIFICATION - STATUS CHANGE
        try
        {
            var emailSubject = $"Ticket Status Updated: {ticket.TicketNumber}";
            var emailBody = $@"
Dear {ticket.CreatedByUser.FirstName},

Your ticket status has been updated.

Ticket Details:
- Ticket Number: {ticket.TicketNumber}
- Title: {ticket.Title}
- Previous Status: {oldStatus}
- New Status: {dto.Status}
- Updated Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}

Thank you for your patience.

Best regards,
Ticketing System
";
            await _emailService.SendEmailAsync(ticket.CreatedByUser.Email, emailSubject, emailBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send ticket status change email: {ex.Message}");
        }

        return MapToTicketResponseDto(ticket);
    }

    public async Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId)
    {
        var ticket = await _context.Tickets
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var oldStatus = ticket.Status.ToString();
        ticket.Status = TicketStatus.Closed;
        ticket.ClosedByUserId = closedByUserId;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, closedByUserId, "Ticket Closed", oldStatus, "Closed");

        // ✅ SEND EMAIL NOTIFICATION - TICKET CLOSED
        try
        {
            var emailSubject = $"Ticket Closed: {ticket.TicketNumber}";
            var emailBody = $@"
Dear {ticket.CreatedByUser.FirstName},

Your ticket has been closed.

Ticket Details:
- Ticket Number: {ticket.TicketNumber}
- Title: {ticket.Title}
- Status: Closed
- Closed Date: {ticket.ClosedAt:yyyy-MM-dd HH:mm:ss}

If you need further assistance, feel free to create a new ticket.

Best regards,
Ticketing System
";
            await _emailService.SendEmailAsync(ticket.CreatedByUser.Email, emailSubject, emailBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send ticket closed email: {ex.Message}");
        }

        return true;
    }

    public async Task<CommentResponseDto> AddCommentAsync(Guid ticketId, CreateCommentDto dto, Guid userId)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = userId,
            Comment = dto.Comment,
            AttachmentUrl = dto.AttachmentUrl ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _context.TicketComments.AddAsync(comment);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, userId, "Comment Added", null, dto.Comment.Substring(0, Math.Min(50, dto.Comment.Length)));

        return new CommentResponseDto
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            UserId = comment.UserId,
            UserName = $"{user.FirstName} {user.LastName}",
            Comment = comment.Comment,
            AttachmentUrl = comment.AttachmentUrl,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task<List<CommentResponseDto>> GetCommentsAsync(Guid ticketId)
    {
        var comments = await _context.TicketComments
            .Include(c => c.User)
            .Where(c => c.TicketId == ticketId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentResponseDto
            {
                Id = c.Id,
                TicketId = c.TicketId,
                UserId = c.UserId,
                UserName = $"{c.User.FirstName} {c.User.LastName}",
                Comment = c.Comment,
                AttachmentUrl = c.AttachmentUrl,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return comments;
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, Guid userId)
    {
        var comment = await _context.TicketComments.FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
            throw new InvalidOperationException("Comment not found");

        if (comment.UserId != userId)
            throw new InvalidOperationException("You can only delete your own comments");

        _context.TicketComments.Remove(comment);
        await _context.SaveChangesAsync();

        await LogActivityAsync(comment.TicketId, userId, "Comment Deleted", comment.Comment.Substring(0, Math.Min(50, comment.Comment.Length)), null);

        return true;
    }

    public async Task<List<ActivityLogResponseDto>> GetTicketActivityAsync(Guid ticketId)
    {
        var activities = await _context.TicketActivityLogs
            .Where(a => a.TicketId == ticketId)
            .Include(a => a.ChangedByUser)
            .OrderByDescending(a => a.ChangedAt)
            .Select(a => new ActivityLogResponseDto
            {
                Id = a.Id,
                TicketId = a.TicketId,
                ChangedByUserId = a.ChangedByUserId,
                ChangedByUserName = a.ChangedByUser != null ? $"{a.ChangedByUser.FirstName} {a.ChangedByUser.LastName}" : "System",
                Action = a.Action,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                ChangedAt = a.ChangedAt
            })
            .ToListAsync();

        return activities;
    }

    private string GenerateTicketNumber()
    {
        var timestamp = DateTime.UtcNow.Ticks.ToString().Substring(0, 8);
        var random = new Random().Next(1000, 9999);
        return $"TKT-{timestamp}-{random}";
    }

    // IMPORTANT: changedByUserId is Guid? (nullable) - accepts both Guid and null
    private async Task LogActivityAsync(Guid ticketId, Guid? changedByUserId, string action, string? oldValue, string? newValue)
    {
        var activityLog = new TicketActivityLog
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedByUserId = changedByUserId,
            Action = action,
            OldValue = oldValue ?? string.Empty,
            NewValue = newValue ?? string.Empty,
            ChangedAt = DateTime.UtcNow
        };

        await _context.TicketActivityLogs.AddAsync(activityLog);
        await _context.SaveChangesAsync();
    }

    private TicketResponseDto MapToTicketResponseDto(Tickets ticket)
    {
        return new TicketResponseDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Description = ticket.Description,
            Category = ticket.Category,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedByUserId = ticket.CreatedByUserId,
            CreatedByUserName = $"{ticket.CreatedByUser.FirstName} {ticket.CreatedByUser.LastName}",
            AssignedToUserId = ticket.AssignedToUserId,
            AssignedToUserName = ticket.AssignedToUser != null ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}" : "Unassigned",
            AssignedByUserId = ticket.AssignedByUserId,
            AssignedByUserName = ticket.AssignedByUser != null ? $"{ticket.AssignedByUser.FirstName} {ticket.AssignedByUser.LastName}" : null,
            ClosedByUserId = ticket.ClosedByUserId,
            ClosedByUserName = ticket.ClosedByUser != null ? $"{ticket.ClosedByUser.FirstName} {ticket.ClosedByUser.LastName}" : null,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            CommentCount = ticket.Comments?.Count ?? 0,
            ActivityLogCount = ticket.ActivityLogs?.Count ?? 0
        };
    }
}