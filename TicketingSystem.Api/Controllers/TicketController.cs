using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Comments;
using TicketingSystem.Api.Dtos.Common;
using TicketingSystem.Api.DTOs.ActivityLogs;
using TicketingSystem.Api.DTOs.Tickets;
using TicketingSystem.Api.Models;
using TicketingSystem.Api.Services;
using System.Security.Claims;
using TicketingSystem.Api.Dtos.Tickets;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly AppDbContext _context;
    private readonly IPdfService _pdfService;

    public TicketsController(ITicketService ticketService, AppDbContext context, IPdfService pdfService)
    {
        _ticketService = ticketService;
        _context = context;
        _pdfService = pdfService;

    }

    // ============ HELPER METHODS ============
    
    /// <summary>
    /// Extract user ID from JWT claims
    /// Reads the nameidentifier claim and parses it as Guid
    /// </summary>
    private Guid GetUserIdFromToken()
    {
        // Get the nameidentifier claim (this is the user ID in JWT)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) 
                          ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        
        if (userIdClaim == null)
            throw new UnauthorizedAccessException("User ID not found in token. No nameidentifier claim.");

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException($"Invalid user ID format in token: {userIdClaim.Value}");

        return userId;
    }

    /// <summary>
    /// Extract user role from JWT claims
    /// Reads the role claim and parses it as UserRole
    /// Defaults to Customer if not found
    /// </summary>
    private UserRole GetUserRoleFromToken()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (string.IsNullOrEmpty(roleClaim))
            return UserRole.Customer;  // Default if no role claim found
        
        if (Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var userRole))
            return userRole;
        
        return UserRole.Customer;  // Default if role parsing fails
    }

    // ============ CREATE TICKET ============

    /// <summary>
    /// POST /api/tickets
    /// Create a new ticket
    /// Frontend expects: { id, ticketNumber, title, status, createdAt }
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<CreateTicketResponseDto>>> CreateTicket([FromBody] CreateTicketDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<CreateTicketResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var userId = GetUserIdFromToken();

            var result = await _ticketService.CreateTicketAsync(dto, userId);

            // ✅ Ensure response includes all frontend-required fields
            return Ok(new ApiResponseDto<CreateTicketResponseDto>
            {
                Success = true,
                Message = "Ticket created successfully",
                Data = result, // Make sure CreateTicketResponseDto has: id, ticketNumber, title, status, createdAt
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<CreateTicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<CreateTicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<CreateTicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while creating the ticket",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ============ GET TICKETS ============

    /// <summary>
    /// GET /api/tickets/{id}
    /// Get a single ticket by ID with role-based access control
    /// 
    /// UPDATED: Now checks if user has permission to view this ticket
    /// - Customer: can only see tickets they created
    /// - Technician: can only see tickets assigned to them
    /// - TeamLead: can see tickets they created or assigned to their team
    /// - SuperAdmin: can see all tickets
    /// - Support: can see all tickets
    /// 
    /// Returns 403 Forbidden if user doesn't have access
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<TicketResponseDto>>> GetTicketById([FromRoute] Guid id)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            // ✅ NEW: Call overload with role-based access control
            var result = await _ticketService.GetTicketByIdAsync(id, userId, userRole);

            return Ok(new ApiResponseDto<TicketResponseDto>
            {
                Success = true,
                Message = "Ticket retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            // ✅ NEW: Return 403 Forbidden for permission denied
            if (ex.Message.Contains("permission"))
                return StatusCode(403, new ApiResponseDto<TicketResponseDto>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });

            return NotFound(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while retrieving the ticket",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// GET /api/tickets
    /// Get all tickets (paginated)
    /// NOTE: Returns all tickets regardless of role (see GetMyTickets for role-filtered)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>>> GetAllTickets(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _ticketService.GetAllTicketsAsync(pageNumber, pageSize);

            return Ok(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = true,
                Message = "Tickets retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving tickets",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// GET /api/tickets/by-status/{status}
    /// Get tickets by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>>> GetTicketsByStatus(
        [FromRoute] TicketStatus status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _ticketService.GetTicketsByStatusAsync(status, pageNumber, pageSize);

            return Ok(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = true,
                Message = $"Tickets with status {status} retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving tickets",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// GET /api/tickets/my-tickets
    /// Get tickets based on user role with role-based filtering
    /// 
    /// UPDATED: Now filters tickets per user's role:
    /// - Customer: sees tickets they created only
    /// - Technician: sees tickets assigned to them (not closed)
    /// - TeamLead: sees tickets they created + team's tickets
    /// - SuperAdmin: sees all tickets
    /// - Support: sees all tickets
    /// </summary>
    [HttpGet("my-tickets")]
    public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>>> GetMyTickets(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            // ✅ NEW: Pass role parameter to GetMyTicketsAsync for role-based filtering
            var result = await _ticketService.GetMyTicketsAsync(userId, pageNumber, pageSize, userRole);

            return Ok(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = true,
                Message = "Your tickets retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving your tickets",
                Errors = new List<string> { ex.Message }
            });
        }
    }


    /// <summary>
/// GET /api/tickets/support-queue
/// Support queue: open tickets that have not yet been assigned.
/// Only Support and SuperAdmin can access this.
/// </summary>
[HttpGet("support-queue")]
public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>>> GetSupportQueue(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
        {
            try
            {
                _ = GetUserIdFromToken();
                var userRole = GetUserRoleFromToken();

                if (userRole != UserRole.Support && userRole != UserRole.SuperAdmin)
                {
                    return StatusCode(403, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
                    {
                        Success = false,
                        Message = "Only Support and SuperAdmin can access the support queue",
                        Data = null,
                        Errors = new List<string> { "Forbidden" }
                    });
                }

                var result = await _ticketService.GetSupportQueueAsync(pageNumber, pageSize);

                return Ok(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
                {
                    Success = true,
                    Message = "Support queue retrieved successfully",
                    Data = result,
                    Errors = new List<string>()
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving the support queue",
                    Errors = new List<string> { ex.Message }
                });
            }
        }



    /// <summary>
    /// GET /api/tickets/assigned-to-me
    /// Get tickets assigned to current user
    /// </summary>
    [HttpGet("assigned-to-me")]
    public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>>> GetAssignedToMe(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetUserIdFromToken();

            var result = await _ticketService.GetAssignedToMeAsync(userId, pageNumber, pageSize);

            return Ok(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = true,
                Message = "Tickets assigned to you retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<PaginatedResponseDto<TicketListItemDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving assigned tickets",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ============ UPDATE TICKET ============

    /// <summary>
    /// PUT /api/tickets/{id}
    /// Update ticket details
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponseDto<TicketResponseDto>>> UpdateTicket(
        [FromRoute] Guid id,
        [FromBody] UpdateTicketDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<TicketResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _ticketService.UpdateTicketAsync(id, dto);

            return Ok(new ApiResponseDto<TicketResponseDto>
            {
                Success = true,
                Message = "Ticket updated successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while updating the ticket",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// PATCH /api/tickets/{id}/assign
    /// Assign ticket to a technician
    /// </summary>
    [HttpPatch("{id}/assign")]
    public async Task<ActionResult<ApiResponseDto<TicketResponseDto>>> AssignTicket(
        [FromRoute] Guid id,
        [FromBody] AssignTicketDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<TicketResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            //var userId = GetUserIdFromToken();

            //var result = await _ticketService.AssignTicketAsync(id, dto, userId);
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            var result = await _ticketService.AssignTicketAsync(id, dto, userId, userRole);

            return Ok(new ApiResponseDto<TicketResponseDto>
            {
                Success = true,
                Message = "Ticket assigned successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }

        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while assigning the ticket",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// PATCH /api/tickets/{id}/status
    /// Change ticket status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponseDto<TicketResponseDto>>> ChangeStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeTicketStatusDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<TicketResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

           // var result = await _ticketService.ChangeStatusAsync(id, dto);
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            var result = await _ticketService.ChangeStatusAsync(id, dto, userId, userRole);

            return Ok(new ApiResponseDto<TicketResponseDto>
            {
                Success = true,
                Message = "Ticket status changed successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while changing the status",
                Errors = new List<string> { ex.Message }
            });
        }

        // catch (InvalidOperationException ex)
        // {
        //     return NotFound(new ApiResponseDto<TicketResponseDto>
        //     {
        //         Success = false,
        //         Message = ex.Message,
        //         Errors = new List<string> { ex.Message }
        //     });
        // }
        // catch (Exception ex)
        // {
        //     return StatusCode(500, new ApiResponseDto<TicketResponseDto>
        //     {
        //         Success = false,
        //         Message = "An error occurred while changing the status",
        //         Errors = new List<string> { ex.Message }
        //     });
        // }
    }


    /// <summary>
/// DELETE /api/tickets/{id}
/// Close a ticket
/// Only TeamLead and SuperAdmin can close tickets.
/// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponseDto<string>>> CloseTicket([FromRoute] Guid id)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            var result = await _ticketService.CloseTicketAsync(id, userId, userRole);

            return Ok(new ApiResponseDto<string>
            {
                Success = true,
                Message = "Ticket closed successfully",
                Data = "Ticket has been closed",
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<string>
            {
                Success = false,
                Message = "An error occurred while closing the ticket",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // /// <summary>
    // /// DELETE /api/tickets/{id}
    // /// Close a ticket
    // /// </summary>
    // [HttpDelete("{id}")]
    // public async Task<ActionResult<ApiResponseDto<string>>> CloseTicket([FromRoute] Guid id)
    // {
    //     try
    //     {
    //         var userId = GetUserIdFromToken();

    //         var result = await _ticketService.CloseTicketAsync(id, userId);

    //         return Ok(new ApiResponseDto<string>
    //         {
    //             Success = true,
    //             Message = "Ticket closed successfully",
    //             Data = "Ticket has been closed",
    //             Errors = new List<string>()
    //         });
    //     }
    //     catch (UnauthorizedAccessException ex)
    //     {
    //         return Unauthorized(new ApiResponseDto<string>
    //         {
    //             Success = false,
    //             Message = ex.Message,
    //             Errors = new List<string> { ex.Message }
    //         });
    //     }
    //     catch (InvalidOperationException ex)
    //     {
    //         return NotFound(new ApiResponseDto<string>
    //         {
    //             Success = false,
    //             Message = ex.Message,
    //             Errors = new List<string> { ex.Message }
    //         });
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, new ApiResponseDto<string>
    //         {
    //             Success = false,
    //             Message = "An error occurred while closing the ticket",
    //             Errors = new List<string> { ex.Message }
    //         });
    //     }
    // }

    // ============ COMMENTS ============

    /// <summary>
    /// POST /api/tickets/{id}/comments
    /// Add comment to ticket
    /// </summary>
    [HttpPost("{id}/comments")]
    public async Task<ActionResult<ApiResponseDto<CommentResponseDto>>> AddComment(
        [FromRoute] Guid id,
        [FromBody] CreateCommentDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<CommentResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var userId = GetUserIdFromToken();

            var result = await _ticketService.AddCommentAsync(id, dto, userId);

            return Ok(new ApiResponseDto<CommentResponseDto>
            {
                Success = true,
                Message = "Comment added successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<CommentResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<CommentResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<CommentResponseDto>
            {
                Success = false,
                Message = "An error occurred while adding the comment",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// GET /api/tickets/{id}/comments
    /// Get all comments for a ticket
    /// </summary>
    [HttpGet("{id}/comments")]
    public async Task<ActionResult<ApiResponseDto<List<CommentResponseDto>>>> GetComments([FromRoute] Guid id)
    {
        try
        {
            var result = await _ticketService.GetCommentsAsync(id);

            return Ok(new ApiResponseDto<List<CommentResponseDto>>
            {
                Success = true,
                Message = "Comments retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<List<CommentResponseDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving comments",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// DELETE /api/tickets/comments/{id}
    /// Delete a comment
    /// </summary>
    [HttpDelete("comments/{id}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteComment([FromRoute] Guid id)
    {
        try
        {
            var userId = GetUserIdFromToken();

            var result = await _ticketService.DeleteCommentAsync(id, userId);

            return Ok(new ApiResponseDto<string>
            {
                Success = true,
                Message = "Comment deleted successfully",
                Data = "Comment has been deleted",
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<string>
            {
                Success = false,
                Message = "An error occurred while deleting the comment",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ============ ACTIVITY ============

    /// <summary>
    /// GET /api/tickets/{id}/activity
    /// Get ticket activity log
    /// </summary>
    [HttpGet("{id}/activity")]
    public async Task<ActionResult<ApiResponseDto<List<ActivityLogResponseDto>>>> GetTicketActivity([FromRoute] Guid id)
    {
        try
        {
            var result = await _ticketService.GetTicketActivityAsync(id);

            return Ok(new ApiResponseDto<List<ActivityLogResponseDto>>
            {
                Success = true,
                Message = "Ticket activity retrieved successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<List<ActivityLogResponseDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving activity",
                Errors = new List<string> { ex.Message }
            });
        }
    }
    [HttpPatch("{id}/priority")]
    public async Task<ActionResult<ApiResponseDto<TicketResponseDto>>> UpdatePriority(
        [FromRoute] Guid id,
        [FromBody] UpdateTicketPriorityDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<TicketResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });

            var userId = GetUserIdFromToken();
            var userRole = GetUserRoleFromToken();

            var result = await _ticketService.UpdatePriorityAsync(id, dto, userId, userRole);

            return Ok(new ApiResponseDto<TicketResponseDto>
            {
                Success = true,
                Message = "Ticket priority updated successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<TicketResponseDto>
            {
                Success = false,
                Message = "An error occurred while updating ticket priority",
                Errors = new List<string> { ex.Message }
            });
        }
    }


    // ==================== ADD THESE ENDPOINTS TO YOUR EXISTING TicketsController ====================
// Also add: private readonly IPdfService _pdfService; to constructor injection
// Update constructor: public TicketsController(ITicketService ticketService, AppDbContext context, IPdfService pdfService)

#region PDF Downloads

/// <summary>
/// GET /api/tickets/{id}/download
/// Download ticket details as PDF.
/// Accessible by: SuperAdmin, Support, TeamLead (own team), Technician (assigned), Customer (own)
/// </summary>
[HttpGet("{id}/download")]
public async Task<IActionResult> DownloadTicketPdf([FromRoute] Guid id)
{
    try
    {
        var userId = GetUserIdFromToken();
        var userRole = GetUserRoleFromToken();

        var canDownload = await _ticketService.CanUserDownloadTicketAsync(id, userId, userRole);
        if (!canDownload)
            return StatusCode(403, new ApiResponseDto<string>
            {
                Success = false,
                Message = "You don't have permission to download this ticket",
                Errors = new List<string> { "Forbidden" }
            });

        var pdfBytes = await _pdfService.GenerateTicketPdfAsync(id);
        var ticket = await _ticketService.GetTicketByIdAsync(id, userId, userRole);

        return File(pdfBytes, "application/pdf", $"Ticket_{ticket.TicketNumber}.pdf");
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new ApiResponseDto<string>
        {
            Success = false,
            Message = ex.Message,
            Errors = new List<string> { ex.Message }
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponseDto<string>
        {
            Success = false,
            Message = "An error occurred while generating the PDF",
            Errors = new List<string> { ex.Message }
        });
    }
}

/// <summary>
/// GET /api/tickets/{id}/download-with-report
/// Download ticket + technician report as a combined PDF.
/// Accessible by: SuperAdmin, Support, TeamLead (own team), Technician (assigned)
/// NOT accessible by Customers
/// </summary>
[HttpGet("{id}/download-with-report")]
public async Task<IActionResult> DownloadTicketWithReportPdf([FromRoute] Guid id)
{
    try
    {
        var userId = GetUserIdFromToken();
        var userRole = GetUserRoleFromToken();

        // Check ticket download permission
        var canDownload = await _ticketService.CanUserDownloadTicketAsync(id, userId, userRole);
        if (!canDownload)
            return StatusCode(403, new ApiResponseDto<string>
            {
                Success = false,
                Message = "You don't have permission to download this ticket",
                Errors = new List<string> { "Forbidden" }
            });

        // Customers cannot download internal reports
        if (userRole == UserRole.Customer)
            return StatusCode(403, new ApiResponseDto<string>
            {
                Success = false,
                Message = "Customers cannot download internal reports",
                Errors = new List<string> { "Forbidden" }
            });

        var pdfBytes = await _pdfService.GenerateTicketWithReportPdfAsync(id);
        var ticket = await _ticketService.GetTicketByIdAsync(id, userId, userRole);

        return File(pdfBytes, "application/pdf", $"Ticket_{ticket.TicketNumber}_Full_Report.pdf");
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new ApiResponseDto<string>
        {
            Success = false,
            Message = ex.Message,
            Errors = new List<string> { ex.Message }
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponseDto<string>
        {
            Success = false,
            Message = "An error occurred while generating the PDF",
            Errors = new List<string> { ex.Message }
        });
    }
}

#endregion

#region Ticket Reports (Technician Resolution Reports)

/// <summary>
/// POST /api/tickets/{id}/report
/// Submit a technician resolution report for a ticket.
/// Only the assigned technician can submit.
/// Can be called at any stage before the ticket is closed.
/// </summary>
[HttpPost("{id}/report")]
public async Task<ActionResult<ApiResponseDto<TicketReportResponseDto>>> SubmitReport(
    [FromRoute] Guid id,
    [FromBody] CreateTicketReportDto dto)
{
    try
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponseDto<TicketReportResponseDto>
            {
                Success = false,
                Message = "Invalid input",
                Errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });

        var userId = GetUserIdFromToken();
        var userRole = GetUserRoleFromToken();

        // Only technicians can submit reports
        if (userRole != UserRole.Technician)
            return StatusCode(403, new ApiResponseDto<TicketReportResponseDto>
            {
                Success = false,
                Message = "Only technicians can submit resolution reports",
                Errors = new List<string> { "Forbidden" }
            });

        var result = await _ticketService.SubmitTicketReportAsync(id, dto, userId);

        return Ok(new ApiResponseDto<TicketReportResponseDto>
        {
            Success = true,
            Message = "Report submitted successfully",
            Data = result,
            Errors = new List<string>()
        });
    }
    catch (UnauthorizedAccessException ex)
    {
        return StatusCode(403, new ApiResponseDto<TicketReportResponseDto>
        {
            Success = false,
            Message = ex.Message,
            Errors = new List<string> { ex.Message }
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new ApiResponseDto<TicketReportResponseDto>
        {
            Success = false,
            Message = ex.Message,
            Errors = new List<string> { ex.Message }
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponseDto<TicketReportResponseDto>
        {
            Success = false,
            Message = "An error occurred while submitting the report",
            Errors = new List<string> { ex.Message }
        });
    }
}

/// <summary>
/// GET /api/tickets/{id}/report
/// Get the technician resolution report for a ticket.
/// Accessible by: SuperAdmin, Support, TeamLead (own team), Technician (own report)
/// NOT accessible by Customers
/// </summary>
[HttpGet("{id}/report")]
public async Task<ActionResult<ApiResponseDto<TicketReportResponseDto>>> GetReport([FromRoute] Guid id)
{
    try
    {
        var userId = GetUserIdFromToken();
        var userRole = GetUserRoleFromToken();

        var canView = await _ticketService.CanUserViewReportAsync(id, userId, userRole);
        if (!canView)
            return StatusCode(403, new ApiResponseDto<TicketReportResponseDto>
            {
                Success = false,
                Message = "You don't have permission to view this report",
                Errors = new List<string> { "Forbidden" }
            });

        var result = await _ticketService.GetTicketReportAsync(id);

        if (result == null)
            return NotFound(new ApiResponseDto<TicketReportResponseDto>
            {
                Success = false,
                Message = "No report found for this ticket",
                Errors = new List<string> { "Report not found" }
            });

        return Ok(new ApiResponseDto<TicketReportResponseDto>
        {
            Success = true,
            Message = "Report retrieved successfully",
            Data = result,
            Errors = new List<string>()
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponseDto<TicketReportResponseDto>
        {
            Success = false,
            Message = "An error occurred while retrieving the report",
            Errors = new List<string> { ex.Message }
        });
    }
}

/// <summary>
/// GET /api/tickets/{id}/report/download
/// Download the technician resolution report as a standalone PDF.
/// Accessible by: SuperAdmin, Support, TeamLead (own team), Technician (own report)
/// </summary>
[HttpGet("{id}/report/download")]
public async Task<IActionResult> DownloadReportPdf([FromRoute] Guid id)
{
    try
    {
        var userId = GetUserIdFromToken();
        var userRole = GetUserRoleFromToken();

        var canView = await _ticketService.CanUserViewReportAsync(id, userId, userRole);
        if (!canView)
            return StatusCode(403, new ApiResponseDto<string>
            {
                Success = false,
                Message = "You don't have permission to download this report",
                Errors = new List<string> { "Forbidden" }
            });

        var report = await _ticketService.GetTicketReportAsync(id);
        if (report == null)
            return NotFound(new ApiResponseDto<string>
            {
                Success = false,
                Message = "No report found for this ticket",
                Errors = new List<string> { "Report not found" }
            });

        var pdfBytes = await _pdfService.GenerateReportPdfAsync(report.Id);

        var ticket = await _ticketService.GetTicketByIdAsync(id, userId, userRole);
        return File(pdfBytes, "application/pdf", $"Report_{ticket.TicketNumber}.pdf");
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new ApiResponseDto<string>
        {
            Success = false,
            Message = ex.Message,
            Errors = new List<string> { ex.Message }
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponseDto<string>
        {
            Success = false,
            Message = "An error occurred while generating the report PDF",
            Errors = new List<string> { ex.Message }
        });
    }
}

#endregion
}