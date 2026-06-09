using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Comments;
using TicketingSystem.Api.Dtos.Common;
using TicketingSystem.Api.Dtos.Tickets;
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
    Task<PaginatedResponseDto<TicketListItemDto>> GetSupportQueueAsync(int pageNumber = 1, int pageSize = 10);

    Task<TicketResponseDto> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto);
    //Task<TicketResponseDto> AssignTicketAsync(Guid ticketId, AssignTicketDto dto, Guid assignedByUserId);
    //Task<TicketResponseDto> ChangeStatusAsync(Guid ticketId, ChangeTicketStatusDto dto);
    //Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId);
    Task<TicketResponseDto> AssignTicketAsync(Guid ticketId, AssignTicketDto dto, Guid assignedByUserId, UserRole assignedByUserRole);
    Task<TicketResponseDto> ChangeStatusAsync(Guid ticketId, ChangeTicketStatusDto dto, Guid changedByUserId, UserRole changedByUserRole);
    Task<TicketResponseDto> UpdatePriorityAsync(Guid ticketId, UpdateTicketPriorityDto dto, Guid changedByUserId, UserRole changedByUserRole);
    //Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId, UserRole closedByUserRole);
    Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId, UserRole closedByUserRole);


    Task<CommentResponseDto> AddCommentAsync(Guid ticketId, CreateCommentDto dto, Guid userId);
    Task<List<CommentResponseDto>> GetCommentsAsync(Guid ticketId);
    Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);
    Task<List<ActivityLogResponseDto>> GetTicketActivityAsync(Guid ticketId);


Task<TicketReportResponseDto> SubmitTicketReportAsync(Guid ticketId, CreateTicketReportDto dto, Guid technicianUserId);
Task<TicketReportResponseDto?> GetTicketReportAsync(Guid ticketId);
Task<bool> CanUserDownloadTicketAsync(Guid ticketId, Guid userId, UserRole userRole);
Task<bool> CanUserViewReportAsync(Guid ticketId, Guid userId, UserRole userRole);
}

public class TicketService : ITicketService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    //private readonly ITicketSmsNotificationService _ticketSmsNotificationService;
    private readonly ITicketSmsQueue _ticketSmsQueue;

    public TicketService(AppDbContext context, IEmailService emailService, IPdfService pdfService, ITicketSmsQueue ticketSmsQueue)
    {
        _context = context;
        _emailService = emailService;
        _pdfService = pdfService;
        // _ticketSmsNotificationService = ticketSmsNotificationService;
        _ticketSmsQueue = ticketSmsQueue;
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

        
        //  SEND SMS NOTIFICATION - TICKET CREATION
        {
                _ticketSmsQueue.Enqueue(
                new TicketSmsNotification(
                    ticket.Id,
                    ticket.TicketNumber,
                    ticket.Title,
                    user.FirstName,
                    user.PhoneNumber,
                    null,
                    TicketStatus.Open,
                    $"ticket:{ticket.Id}:created"));
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

   public async Task<TicketResponseDto> AssignTicketAsync(Guid ticketId, AssignTicketDto dto, Guid assignedByUserId, UserRole assignedByUserRole)
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

        if (technician.Role != UserRole.Technician)
            throw new InvalidOperationException("Selected user is not a technician");

        if (assignedByUserRole == UserRole.Support)
        {
            if (!dto.TeamLeadUserId.HasValue)
                throw new InvalidOperationException("Support must select a TeamLead before assigning a technician");

            var teamLeadExists = await _context.Users.AnyAsync(u =>
                u.Id == dto.TeamLeadUserId.Value &&
                u.Role == UserRole.TeamLead);

            if (!teamLeadExists)
                throw new InvalidOperationException("Selected TeamLead was not found");

            if (technician.TeamLeadUserId != dto.TeamLeadUserId.Value)
                throw new InvalidOperationException("Selected technician does not belong to the selected TeamLead");
        }
        else if (assignedByUserRole == UserRole.TeamLead)
        {
            if (technician.TeamLeadUserId != assignedByUserId)
                throw new UnauthorizedAccessException("You can only assign tickets to technicians in your team");

            var ticketBelongsToTeam = ticket.AssignedToUserId.HasValue &&
                await _context.Users.AnyAsync(u =>
                    u.Id == ticket.AssignedToUserId.Value &&
                    u.TeamLeadUserId == assignedByUserId);

            if (!ticketBelongsToTeam && ticket.CreatedByUserId != assignedByUserId)
                throw new UnauthorizedAccessException("You can only reassign tickets already assigned to your team");
        }
        else if (assignedByUserRole != UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to assign tickets");
        }

        var oldAssignedTo = ticket.AssignedToUser != null
            ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}"
            : "Unassigned";

        var newAssignedTo = $"{technician.FirstName} {technician.LastName}";

        ticket.AssignedToUserId = dto.AssignedToUserId;
        ticket.AssignedByUserId = assignedByUserId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, assignedByUserId, "Ticket Assigned", oldAssignedTo, newAssignedTo);

        return MapToTicketResponseDto(ticket);
    }



        public async Task<TicketResponseDto> ChangeStatusAsync(Guid ticketId, ChangeTicketStatusDto dto, Guid changedByUserId, UserRole changedByUserRole)
        {
            var ticket = await _context.Tickets
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .Include(t => t.ClosedByUser)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                throw new InvalidOperationException("Ticket not found");

            switch (changedByUserRole)
            {
                case UserRole.Technician:
                    if (ticket.AssignedToUserId != changedByUserId)
                        throw new UnauthorizedAccessException("You can only update tickets assigned to you");

                    if (dto.Status != TicketStatus.InProgress && dto.Status != TicketStatus.Resolved)
                        throw new UnauthorizedAccessException("Technicians can only mark tickets as In Progress or Resolved");
                    break;

                case UserRole.TeamLead:
                    var belongsToTeam = ticket.AssignedToUserId.HasValue &&
                        await _context.Users.AnyAsync(u =>
                            u.Id == ticket.AssignedToUserId.Value &&
                            u.TeamLeadUserId == changedByUserId);

                    if (!belongsToTeam && ticket.CreatedByUserId != changedByUserId)
                        throw new UnauthorizedAccessException("You can only update tickets assigned to your team");

                    if (dto.Status == TicketStatus.Closed)
                    {
                        ticket.ClosedByUserId = changedByUserId;
                        ticket.ClosedAt = DateTime.UtcNow;
                    }
                    break;

                case UserRole.Customer:
                    if (ticket.CreatedByUserId != changedByUserId)
                        throw new UnauthorizedAccessException("You can only reopen your own tickets");

                    if (ticket.Status != TicketStatus.Resolved || dto.Status != TicketStatus.Reopened)
                        throw new UnauthorizedAccessException("Customers can only reopen resolved tickets");
                    break;

                case UserRole.Support:
                    if (dto.Status == TicketStatus.Closed)
                        throw new UnauthorizedAccessException("Support cannot close tickets");
                    break;

                case UserRole.SuperAdmin:
                    if (dto.Status == TicketStatus.Closed)
                    {
                        ticket.ClosedByUserId = changedByUserId;
                        ticket.ClosedAt = DateTime.UtcNow;
                    }
                    break;

                default:
                    throw new UnauthorizedAccessException("You do not have permission to change ticket status");
            }

            var oldStatus = ticket.Status;
            ticket.Status = dto.Status;
            ticket.UpdatedAt = DateTime.UtcNow;

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            await LogActivityAsync(ticketId, changedByUserId, "Status Changed", oldStatus.ToString(), dto.Status.ToString());
            
            if (oldStatus != dto.Status)
            {
                _ticketSmsQueue.Enqueue(
                new TicketSmsNotification(
                    ticket.Id,
                    ticket.TicketNumber,
                    ticket.Title,
                    ticket.CreatedByUser.FirstName,
                    ticket.CreatedByUser.PhoneNumber,
                    oldStatus,
                    dto.Status,
                    $"ticket:{ticket.Id}:status:{oldStatus}-to-{dto.Status}"));
            }

            return MapToTicketResponseDto(ticket);
        }



        public async Task<bool> CloseTicketAsync(Guid ticketId, Guid closedByUserId, UserRole closedByUserRole)
        {
            if (closedByUserRole != UserRole.TeamLead && closedByUserRole != UserRole.SuperAdmin)
                throw new UnauthorizedAccessException("Only TeamLead or SuperAdmin can close tickets");

            var ticket = await _context.Tickets
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                throw new InvalidOperationException("Ticket not found");

            if (closedByUserRole == UserRole.TeamLead)
            {
                var belongsToTeam = ticket.AssignedToUserId.HasValue &&
                    await _context.Users.AnyAsync(u =>
                        u.Id == ticket.AssignedToUserId.Value &&
                        u.TeamLeadUserId == closedByUserId);

                if (!belongsToTeam && ticket.CreatedByUserId != closedByUserId)
                    throw new UnauthorizedAccessException("You can only close tickets assigned to your team");
            }

            if (ticket.Status != TicketStatus.Resolved)
                throw new InvalidOperationException("Only resolved tickets can be closed");

            var oldStatus = ticket.Status;

            ticket.Status = TicketStatus.Closed;
            ticket.ClosedByUserId = closedByUserId;
            ticket.ClosedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            await LogActivityAsync(ticketId, closedByUserId, "Ticket Closed", oldStatus.ToString(), TicketStatus.Closed.ToString());

            _ticketSmsQueue.Enqueue(
            new TicketSmsNotification(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.CreatedByUser.FirstName,
                ticket.CreatedByUser.PhoneNumber,
                oldStatus,
                TicketStatus.Closed,
                $"ticket:{ticket.Id}:status:{oldStatus}-to-{TicketStatus.Closed}"));

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
    public async Task<PaginatedResponseDto<TicketListItemDto>> GetSupportQueueAsync(int pageNumber = 1, int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.AssignedToUserId == null && t.Status == TicketStatus.Open)
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
                AssignedToUserName = "Unassigned",
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
    public async Task<TicketResponseDto> UpdatePriorityAsync(Guid ticketId, UpdateTicketPriorityDto dto, Guid changedByUserId, UserRole changedByUserRole)
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

        if (changedByUserRole == UserRole.TeamLead)
        {
            var belongsToTeam = ticket.AssignedToUserId.HasValue &&
                await _context.Users.AnyAsync(u =>
                    u.Id == ticket.AssignedToUserId.Value &&
                    u.TeamLeadUserId == changedByUserId);

            if (!belongsToTeam && ticket.CreatedByUserId != changedByUserId)
                throw new UnauthorizedAccessException("You can only change priority for tickets assigned to your team");
        }
        else if (changedByUserRole != UserRole.Support && changedByUserRole != UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only Support, TeamLead, or SuperAdmin can change ticket priority");
        }

        var oldPriority = ticket.Priority.ToString();

        ticket.Priority = dto.Priority;
        ticket.UpdatedAt = DateTime.UtcNow;

        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, changedByUserId, "Priority Updated", oldPriority, dto.Priority.ToString());

        return MapToTicketResponseDto(ticket);
    }


    #region Report Methods

/// <summary>
/// Technician submits a resolution report for a ticket they are assigned to.
/// Only the currently assigned technician can submit a report.
/// Cannot submit if ticket is already closed.
/// </summary>
public async Task<TicketReportResponseDto> SubmitTicketReportAsync(
    Guid ticketId,
    CreateTicketReportDto dto,
    Guid technicianUserId)
{
    var ticket = await _context.Tickets
        .Include(t => t.AssignedToUser)
        .FirstOrDefaultAsync(t => t.Id == ticketId);

    if (ticket == null)
        throw new InvalidOperationException("Ticket not found");

    if (ticket.Status == TicketStatus.Closed)
        throw new InvalidOperationException("Cannot submit a report for a closed ticket");

    if (ticket.AssignedToUserId != technicianUserId)
        throw new UnauthorizedAccessException("Only the assigned technician can submit a report");

    var technician = await _context.Users.FindAsync(technicianUserId);
    if (technician == null)
        throw new InvalidOperationException("Technician not found");

    // Check if a report already exists for this ticket
    var existingReport = await _context.TicketReports
        .FirstOrDefaultAsync(r => r.TicketId == ticketId);

    if (existingReport != null)
    {
        // Update existing report
        existingReport.WorkSummary = dto.WorkSummary;
        existingReport.ResolutionSteps = dto.ResolutionSteps;
        existingReport.PartsUsed = dto.PartsUsed ?? string.Empty;
        existingReport.TimeSpent = dto.TimeSpent;
        existingReport.Recommendations = dto.Recommendations ?? string.Empty;
        existingReport.IsResolved = dto.IsResolved;
        existingReport.FollowUpNotes = dto.FollowUpNotes;
        existingReport.UpdatedAt = DateTime.UtcNow;

        _context.TicketReports.Update(existingReport);
        await _context.SaveChangesAsync();

        await LogActivityAsync(ticketId, technicianUserId, "Report Updated", null, null);

        return MapToTicketReportResponseDto(existingReport, technician);
    }

    // Create new report
    var report = new TicketReport
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        TechnicianUserId = technicianUserId,
        WorkSummary = dto.WorkSummary,
        ResolutionSteps = dto.ResolutionSteps,
        PartsUsed = dto.PartsUsed ?? string.Empty,
        TimeSpent = dto.TimeSpent,
        Recommendations = dto.Recommendations ?? string.Empty,
        IsResolved = dto.IsResolved,
        FollowUpNotes = dto.FollowUpNotes,
        SubmittedAt = DateTime.UtcNow
    };

    await _context.TicketReports.AddAsync(report);
    await _context.SaveChangesAsync();

    await LogActivityAsync(ticketId, technicianUserId, "Report Submitted", null, $"Time spent: {dto.TimeSpent}");

    return MapToTicketReportResponseDto(report, technician);
}

/// <summary>
/// Get the resolution report for a ticket (returns null if no report exists)
/// </summary>
public async Task<TicketReportResponseDto?> GetTicketReportAsync(Guid ticketId)
{
    var report = await _context.TicketReports
        .Include(r => r.TechnicianUser)
        .FirstOrDefaultAsync(r => r.TicketId == ticketId);

    if (report == null)
        return null;

    return MapToTicketReportResponseDto(report, report.TechnicianUser);
}

/// <summary>
/// Check if a user is allowed to download the ticket as PDF.
/// Rules:
/// - SuperAdmin: all tickets
/// - Support: all tickets (they triage everything)
/// - TeamLead: their team's tickets + tickets they created
/// - Technician: tickets assigned to them
/// - Customer: tickets they created
/// </summary>
public async Task<bool> CanUserDownloadTicketAsync(Guid ticketId, Guid userId, UserRole userRole)
{
    if (userRole == UserRole.SuperAdmin || userRole == UserRole.Support)
        return true;

    var ticket = await _context.Tickets
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == ticketId);

    if (ticket == null)
        return false;

    return userRole switch
    {
        UserRole.Customer => ticket.CreatedByUserId == userId,
        UserRole.Technician => ticket.AssignedToUserId == userId,
        UserRole.TeamLead =>
            ticket.CreatedByUserId == userId ||
            (ticket.AssignedToUserId.HasValue &&
             _context.Users.Any(u => u.Id == ticket.AssignedToUserId.Value && u.TeamLeadUserId == userId)),
        _ => false
    };
}

/// <summary>
/// Check if a user is allowed to view/download the technician report.
/// Rules:
/// - SuperAdmin: all reports
/// - Support: all reports (they oversee the process)
/// - TeamLead: reports for tickets assigned to their team
/// - Technician: their own reports only
/// - Customer: NO access to technician reports (internal document)
/// </summary>
public async Task<bool> CanUserViewReportAsync(Guid ticketId, Guid userId, UserRole userRole)
{
    // Customers don't see internal reports
    if (userRole == UserRole.Customer)
        return false;

    if (userRole == UserRole.SuperAdmin || userRole == UserRole.Support)
        return true;

    var ticket = await _context.Tickets
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == ticketId);

    if (ticket == null)
        return false;

    // TeamLead: can see report if ticket is assigned to their team
    if (userRole == UserRole.TeamLead)
    {
        return ticket.CreatedByUserId == userId ||
               (ticket.AssignedToUserId.HasValue &&
                await _context.Users.AnyAsync(u =>
                    u.Id == ticket.AssignedToUserId.Value &&
                    u.TeamLeadUserId == userId));
    }

    // Technician: only their own reports
    if (userRole == UserRole.Technician)
    {
        return await _context.TicketReports.AnyAsync(r =>
            r.TicketId == ticketId && r.TechnicianUserId == userId);
    }

    return false;
}

#endregion

// ==================== HELPER: Mapping method ====================

/// <summary>
/// Map TicketReport entity to TicketReportResponseDto
/// </summary>
private static TicketReportResponseDto MapToTicketReportResponseDto(TicketReport report, Users technician)
{
    return new TicketReportResponseDto
    {
        Id = report.Id,
        TicketId = report.TicketId,
        TechnicianUserId = report.TechnicianUserId,
        TechnicianName = $"{technician.FirstName} {technician.LastName}",
        WorkSummary = report.WorkSummary,
        ResolutionSteps = report.ResolutionSteps,
        PartsUsed = report.PartsUsed,
        TimeSpent = report.TimeSpent,
        Recommendations = report.Recommendations,
        IsResolved = report.IsResolved,
        FollowUpNotes = report.FollowUpNotes,
        SubmittedAt = report.SubmittedAt,
        UpdatedAt = report.UpdatedAt
    };
}

}




