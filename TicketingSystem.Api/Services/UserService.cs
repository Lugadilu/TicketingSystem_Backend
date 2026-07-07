using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Auth;
using TicketingSystem.Api.Dtos.Users;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;
/// UserService Implementation
/// - Handles user invitations (SuperAdmin → TeamLead, TeamLead → Technician)
/// - Manages team relationships
/// - Provides user lookups
public class UserService : IUserService
{
    // Dependencies injected via constructor
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInvitationService _invitationService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPhoneNumberService _phoneNumberService;

    public UserService(
        AppDbContext context,
        IEmailService emailService,
        IInvitationService invitationService,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IPhoneNumberService phoneNumberService)
    {
        _context = context;
        _emailService = emailService;
        _invitationService = invitationService;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _phoneNumberService = phoneNumberService;
    }
    /// INVITE USER
    /// SuperAdmin invites TeamLeads
    /// TeamLead invites Technicians
    /// 
    /// Flow:
    /// 1. Validate inviter has permission
    /// 2. Check email doesn't already exist
    /// 3. Generate invitation token (7 day expiry)
    /// 4. Create pending user in database
    /// 5. Send invitation email
    public async Task<UserResponseDto> InviteUserAsync(InviteUserDto dto, Guid invitedByUserId)
    {
        // Step 1: Get inviter and validate permissions
        var inviter = await _context.Users.FirstOrDefaultAsync(u => u.Id == invitedByUserId);
        if (inviter == null)
            throw new InvalidOperationException("Inviter not found");

        // SuperAdmin can ONLY invite TeamLeads
        if (inviter.Role == UserRole.SuperAdmin && dto.Role != UserRole.TeamLead)
            throw new InvalidOperationException("SuperAdmin can only invite TeamLeads");

        // TeamLead can ONLY invite Technicians
        if (inviter.Role == UserRole.TeamLead && dto.Role != UserRole.Technician)
            throw new InvalidOperationException("TeamLead can only invite Technicians");

        // Only SuperAdmin and TeamLead can invite
        if (inviter.Role != UserRole.SuperAdmin && inviter.Role != UserRole.TeamLead)
            throw new InvalidOperationException("Only SuperAdmin and TeamLead can invite users");

        // Step 2: Check if email already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
            throw new InvalidOperationException("Email already registered");

        // Step 3: Generate invitation token (valid for 7 days)
        var invitationToken = _invitationService.GenerateInvitationToken();
        var invitationExpiry = _invitationService.GetInvitationExpiryTime();

        // Step 4: Create new pending user
        var newUser = new Users
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = _phoneNumberService.NormalizeForStorage(dto.PhoneNumber),
            Role = dto.Role,
            Status = UserStatus.Pending,  // ← NOT ACTIVE YET
            InvitationToken = invitationToken,
            InvitationExpiresAt = invitationExpiry,
            InvitedByUserId = invitedByUserId,
            // If TeamLead invites Technician, assign Technician to TeamLead
            TeamLeadUserId = (inviter.Role == UserRole.TeamLead) ? invitedByUserId : null,
            IsActive = false,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(newUser);
        await _context.SaveChangesAsync();

        // Step 5: Send invitation email
        var invitationLink = $"https://yourdomain.com/accept-invitation?token={invitationToken}";
        var emailSubject = $"You're invited to Ticketing System as {dto.Role}";
        var emailBody = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2>Welcome to Ticketing System!</h2>
                    <p>Dear {dto.FirstName},</p>
                    <p>You have been invited to join the Ticketing System as a <strong>{dto.Role}</strong>.</p>
                    <p>Click the button below to accept the invitation:</p>
                    <p>
                        <a href='{invitationLink}' style='background-color: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                            Accept Invitation
                        </a>
                    </p>
                    <p><strong>This invitation expires in 7 days.</strong></p>
                    <p>If you didn't expect this, please ignore this email.</p>
                    <p>Best regards,<br/>Ticketing System Team</p>
                </div>
            </body>
            </html>
            ";

        await _emailService.SendEmailAsync(newUser.Email, emailSubject, emailBody);

        return MapToUserResponseDto(newUser, inviter);
    }
    /// ACCEPT INVITATION
    /// User clicks link from email and sets their password
    /// 
    /// Flow:
    /// 1. Find user by invitation token
    /// 2. Check token isn't expired
    /// 3. Check invitation not already accepted
    /// 4. Hash password and activate account
    /// 5. Send welcome email
    /// 6. Generate JWT token for login
    public async Task<LoginResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto) 
    {
        // Step 1: Find user by token
        var user = await _context.Users
            .Include(u => u.InvitedByUser)
            .Include(u => u.TeamLeadUser)
            .FirstOrDefaultAsync(u => u.InvitationToken == dto.InvitationToken);

        if (user == null)
            throw new InvalidOperationException("Invalid invitation token");

        // Step 2: Check if token expired
        if (_invitationService.IsInvitationExpired(user.InvitationExpiresAt))
            throw new InvalidOperationException("Invitation expired. Please request a new one.");

        // Step 3: Check if already accepted
        if (user.InvitationAcceptedAt.HasValue)
            throw new InvalidOperationException("This invitation has already been accepted");

        // Step 4: Hash password and activate
        user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
        user.Status = UserStatus.Active;
        user.InvitationAcceptedAt = DateTime.UtcNow;
        user.InvitationToken = null;  // Clear token
        user.InvitationExpiresAt = null;  // Clear expiry
        user.IsActive = true;
        user.IsEmailVerified = true; 
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Step 5: Send welcome email
        var roleInfo = user.Role == UserRole.Technician && user.TeamLeadUser != null
            ? $"\nTeam Lead: {user.TeamLeadUser.FirstName} {user.TeamLeadUser.LastName}"
            : "";

        var emailSubject = $"Welcome to Ticketing System - {user.Role}";
        var emailBody = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2>Welcome to Ticketing System!</h2>
                    <p>Dear {user.FirstName},</p>
                    <p>Your account has been successfully activated!</p>
                    <div style='background-color: #f0f0f0; padding: 15px; border-left: 4px solid #007bff; margin: 20px 0;'>
                        <p><strong>Your Details:</strong></p>
                        <p>Email: {user.Email}</p>
                        <p>Role: {user.Role}{roleInfo}</p>
                    </div>
                    <p>You can now login at: <a href='https://yourdomain.com/login'>https://yourdomain.com/login</a></p>
                    <p>Best regards,<br/>Ticketing System Team</p>
                </div>
            </body>
            </html>
            ";

        await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);

        // Step 6: Generate JWT token for immediate login
        var token = _jwtTokenService.GenerateToken(user);
        var expiresAt = _jwtTokenService.GetTokenExpiryTime();

        return new LoginResponseDto
        {
            UserId = user.Id,  
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiresAt 
        };
    }
    /// GET TEAM MEMBERS
    /// TeamLead views their Technicians
    /// Returns only active, undeleted technicians
    public async Task<List<UserResponseDto>> GetTeamMembersAsync(Guid teamLeadId)
    {
        // Validate TeamLead exists
        var teamLead = await _context.Users.FirstOrDefaultAsync(u => u.Id == teamLeadId);
        if (teamLead == null)
            throw new InvalidOperationException("User not found");

        if (teamLead.Role != UserRole.TeamLead)
            throw new InvalidOperationException("User is not a TeamLead");

        // Get all Technicians assigned to this TeamLead
        var teamMembers = await _context.Users
            .Where(u => u.TeamLeadUserId == teamLeadId && u.Role == UserRole.Technician && u.IsActive)
            .Include(u => u.TeamLeadUser)
            .ToListAsync();

        return teamMembers.Select(u => MapToUserResponseDto(u, u.TeamLeadUser)).ToList();
    }
    /// GET USER BY ID
    /// Get specific user details 
    public async Task<UserResponseDto> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.InvitedByUser)
            .Include(u => u.TeamLeadUser)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException("User not found");

        return MapToUserResponseDto(user, user.InvitedByUser ?? user.TeamLeadUser);
    }
    /// GET ALL USERS
    /// SuperAdmin views all users with optional role filter
    /// Usage: /api/users?role=TeamLead
    public async Task<List<UserResponseDto>> GetAllUsersAsync(UserRole? roleFilter = null)
    {
        var query = _context.Users
            .Include(u => u.InvitedByUser)
            .Include(u => u.TeamLeadUser)
            .AsQueryable();

        if (roleFilter.HasValue)
            query = query.Where(u => u.Role == roleFilter.Value);

        var users = await query.ToListAsync();
        return users.Select(u => MapToUserResponseDto(u, u.InvitedByUser ?? u.TeamLeadUser)).ToList();
    }
    /// GET USER BY EMAIL
    /// Used for lookups
    public async Task<Users?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// HELPER: Map User model to UserResponseDto
    private UserResponseDto MapToUserResponseDto(Users user, Users? relatedUser = null)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            Status = user.Status,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            InvitationExpiresAt = user.InvitationExpiresAt,
            InvitationAcceptedAt = user.InvitationAcceptedAt,
            TeamLeadUserId = user.TeamLeadUserId,
            InvitedByUserId = user.InvitedByUserId,
            InvitedByUserName = user.InvitedByUser != null
                ? $"{user.InvitedByUser.FirstName} {user.InvitedByUser.LastName}"
                : (relatedUser != null ? $"{relatedUser.FirstName} {relatedUser.LastName}" : null),
            TeamLeadUserName = user.TeamLeadUser != null
                ? $"{user.TeamLeadUser.FirstName} {user.TeamLeadUser.LastName}"
                : null
        };
    }
}