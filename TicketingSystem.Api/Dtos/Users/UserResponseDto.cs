using System;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Dtos.Users;

/// <summary>
/// Response DTO for user data
/// Used in: GetUserById, GetTeamMembers, InviteUser responses
/// Contains: User details + relationships (InvitedBy, TeamLead)
/// </summary>
public class UserResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    
    // Role and Status
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    
    // Account Status
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Invitation Info
    public DateTime? InvitationExpiresAt { get; set; }
    public DateTime? InvitationAcceptedAt { get; set; }

    // Relationship Names (Who invited them, Who is their team lead)
    public string? InvitedByUserName { get; set; }
    public string? TeamLeadUserName { get; set; }
}