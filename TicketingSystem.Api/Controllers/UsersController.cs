using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Api.Dtos.Auth;
using TicketingSystem.Api.Dtos.Users;
using TicketingSystem.Api.Models;
using TicketingSystem.Api.Services;

namespace TicketingSystem.Api.Controllers;
/// Endpoints for invitations and user management
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }
    /// POST /api/users/invite
    /// Invite a new user (SuperAdmin invites TeamLead, TeamLead invites Technician)
    [HttpPost("invite")]
    [Authorize(Roles = "SuperAdmin,TeamLead")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserDto dto)
    {
        try
        {
            // Extract user ID from JWT token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var invitedByUserId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            // Call service to invite user
            var result = await _userService.InviteUserAsync(dto, invitedByUserId);

            return Ok(new
            {
                success = true,
                message = $"Invitation sent to {dto.Email}",
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred", details = ex.Message });
        }
    }
    /// POST /api/users/accept-invitation
    /// Accept invitation and activate user account with password
    /// No authentication required - invitation token is the auth
    /// User sets their password here
    /// Response includes JWT token for login
    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationDto dto)
    {
        try
        {
            // Validate input
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid input", errors = ModelState });

            // Call service to accept invitation
            var result = await _userService.AcceptInvitationAsync(dto);

            return Ok(new
            {
                success = true,
                message = "Invitation accepted successfully. You can now login.",
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred", details = ex.Message });
        }
    }
    /// GET /api/users/team-members
    /// TeamLead gets list of their Technicians
    /// Permissions: TeamLead only
    /// Returns: List of UserResponseDto (their team members)
    [HttpGet("team-members")]
    [Authorize(Roles = "TeamLead")]
    public async Task<IActionResult> GetTeamMembers()
    {
        try
        {
            // Extract TeamLead ID from JWT token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var teamLeadId))
                return Unauthorized(new { success = false, message = "Invalid user token" });

            // Call service to get team members
            var result = await _userService.GetTeamMembersAsync(teamLeadId);

            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred", details = ex.Message });
        }
    }
    /// GET /api/users/{userId}
    /// Get specific user details
    /// Permissions: SuperAdmin, TeamLead
    [HttpGet("{userId}")]
    [Authorize(Roles = "SuperAdmin,TeamLead")]
    public async Task<IActionResult> GetUserById([FromRoute] Guid userId)
    {
        try
        {
            var result = await _userService.GetUserByIdAsync(userId);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred", details = ex.Message });
        }
    }
    /// GET /api/users
    /// Get all users with optional role filter
    /// Permissions: SuperAdmin only
    /// Query Parameters:
    /// - role: Optional role filter (SuperAdmin, TeamLead, Technician, Customer, Support)
    [HttpGet]
    [Authorize(Roles = "SuperAdmin, Support")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? role = null)
    {
        try
        {
            UserRole? roleFilter = null;
            
            // Parse role filter if provided
            if (!string.IsNullOrEmpty(role))
            {
                if (Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
                    roleFilter = parsedRole;
                else
                    return BadRequest(new { success = false, message = "Invalid role value" });
            }

            var result = await _userService.GetAllUsersAsync(roleFilter);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred", details = ex.Message });
        }
    }
}