using System;
using System.Collections.Generic;

namespace TicketingSystem.Api.Models;

public class Users
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsEmailVerified { get; set; }
    public string EmailVerificationOTP { get; set; } = string.Empty;
    public DateTime? OTPExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Password Reset Fields
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }

    // Invitation System Fields
    public UserStatus Status { get; set; } = (UserStatus)0;  // Sentinel value
    public string? InvitationToken { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }
    public DateTime? InvitationAcceptedAt { get; set; }

    // NEW: User Relationships
    // Who invited this user (SuperAdmin invites TeamLead, TeamLead invites Technician)
    public Guid? InvitedByUserId { get; set; }
    public Users? InvitedByUser { get; set; }

    // Which TeamLead manages this user
    public Guid? TeamLeadUserId { get; set; }
    public Users? TeamLeadUser { get; set; }

    // Navigation properties for tickets
    public ICollection<Tickets> CreatedTickets { get; set; } = new List<Tickets>();
    public ICollection<Tickets> AssignedTickets { get; set; } = new List<Tickets>();
    public ICollection<Tickets> AssignedByTickets { get; set; } = new List<Tickets>();
    public ICollection<Tickets> ClosedByTickets { get; set; } = new List<Tickets>();

    // Navigation properties for comments and activity
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketActivityLog> ActivityLogs { get; set; } = new List<TicketActivityLog>();
}