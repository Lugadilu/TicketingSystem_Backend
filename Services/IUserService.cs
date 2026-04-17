using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TicketingSystem.Api.Dtos.Auth;
using TicketingSystem.Api.Dtos.Users;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface IUserService
{
    /// Invite a new user (SuperAdmin invites TeamLead, TeamLead invites Technician)
    Task<UserResponseDto> InviteUserAsync(InviteUserDto dto, Guid invitedByUserId);
    /// Accept invitation and activate user account with password
    Task<LoginResponseDto> AcceptInvitationAsync(Dtos.Auth.AcceptInvitationDto dto);
    /// TeamLead gets list of their Technicians
    Task<List<UserResponseDto>> GetTeamMembersAsync(Guid teamLeadId);
    /// Get specific user by ID (with role check)
    Task<UserResponseDto> GetUserByIdAsync(Guid userId);
    /// Get all users with optional role filter (SuperAdmin only)
    Task<List<UserResponseDto>> GetAllUsersAsync(UserRole? roleFilter = null);
    /// Get user by email address
    Task<Users?> GetUserByEmailAsync(string email);
}