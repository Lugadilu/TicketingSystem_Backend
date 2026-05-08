using System;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Users> Users { get; set; }
    public DbSet<Tickets> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketActivityLog> TicketActivityLogs { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }

     // This method configures relationships and constraints between tables
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Users>(entity =>
        {
            // Primary Key
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Email).IsUnique(); // No two users can have same email

            // Field configurations
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.PasswordHash)
                .IsRequired();

            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(e => e.EmailVerificationOTP)
                .HasMaxLength(6);

            // Enum configuration (store as integer in database)
            entity.Property(e => e.Role)
                .HasConversion<int>();

            // FIX: Status configuration with sentinel value
            entity.Property(e => e.Status)
                .HasConversion<int>()
                .HasDefaultValue(UserStatus.Pending)
                .HasSentinel((UserStatus)0);  // Sentinel value to fix the warning

            // Default values
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.IsEmailVerified)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ===== INVITATION SYSTEM RELATIONSHIPS =====

            // InvitedByUser relationship (who invited this user)
            entity.HasOne(u => u.InvitedByUser)
                .WithMany()  // One user can invite many others
                .HasForeignKey(u => u.InvitedByUserId)
                .OnDelete(DeleteBehavior.SetNull);  // If inviter deleted, just clear the field

            // TeamLeadUser relationship (who is the team lead for this user)
            entity.HasOne(u => u.TeamLeadUser)
                .WithMany()  // One team lead can manage many team members
                .HasForeignKey(u => u.TeamLeadUserId)
                .OnDelete(DeleteBehavior.SetNull);  // If team lead deleted, just clear the field
        });

        // ===== TICKETS TABLE CONFIGURATION =====
        modelBuilder.Entity<Tickets>(entity =>
        {
            // Primary Key
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.TicketNumber).IsUnique(); // No two tickets can have same ticket number

            // Field configurations
            entity.Property(e => e.TicketNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Description)
                .IsRequired();

            entity.Property(e => e.Category)
                .HasMaxLength(100);

            // Enum configurations (store as integer in database)
            entity.Property(e => e.Status)
                .HasConversion<int>();

            entity.Property(e => e.Priority)
                .HasConversion<int>();

            // Default values
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ===== RELATIONSHIPS =====

            // Ticket → CreatedByUser (Many Tickets created by One User)
            entity.HasOne(t => t.CreatedByUser)
                .WithMany(u => u.CreatedTickets)
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);  // Don't allow deleting user if they created tickets

            // Ticket → AssignedToUser (Many Tickets assigned to One User)
            entity.HasOne(t => t.AssignedToUser)
                .WithMany(u => u.AssignedTickets)
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);  // If user deleted, set ticket as unassigned (null)

            // Ticket → AssignedByUser (Many Tickets assigned by One User)
            entity.HasOne(t => t.AssignedByUser)
                .WithMany(u => u.AssignedByTickets)
                .HasForeignKey(t => t.AssignedByUserId)
                .OnDelete(DeleteBehavior.SetNull);  // If user deleted, clear who assigned it

            // Ticket → ClosedByUser (Many Tickets closed by One User)
            entity.HasOne(t => t.ClosedByUser)
                .WithMany(u => u.ClosedByTickets)
                .HasForeignKey(t => t.ClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull);  // If user deleted, clear who closed it
        });

        // ===== TICKETCOMMENT TABLE CONFIGURATION =====
        modelBuilder.Entity<TicketComment>(entity =>
        {
            // Primary Key
            entity.HasKey(e => e.Id);

            // Field configurations
            entity.Property(e => e.Comment)
                .IsRequired();

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // ===== RELATIONSHIPS =====

            // Comment → Ticket (Many Comments on One Ticket)
            entity.HasOne(tc => tc.Ticket)
                .WithMany(t => t.Comments)
                .HasForeignKey(tc => tc.TicketId)
                .OnDelete(DeleteBehavior.Cascade);  // If ticket deleted, delete its comments

            // Comment → User (Many Comments written by One User)
            entity.HasOne(tc => tc.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(tc => tc.UserId)
                .OnDelete(DeleteBehavior.Restrict);  // Don't allow deleting user if they wrote comments
        });

        // ===== TICKETACTIVITYLOG TABLE CONFIGURATION =====
        modelBuilder.Entity<TicketActivityLog>(entity =>
        {
            // Primary Key
            entity.HasKey(e => e.Id);

            // Field configurations
            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.OldValue)
                .HasMaxLength(255);

            entity.Property(e => e.NewValue)
                .HasMaxLength(255);

            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("NOW()");

            // ===== RELATIONSHIPS =====

            // ActivityLog → Ticket (Many Logs for One Ticket)
            entity.HasOne(tal => tal.Ticket)
                .WithMany(t => t.ActivityLogs)
                .HasForeignKey(tal => tal.TicketId)
                .OnDelete(DeleteBehavior.Cascade);  // If ticket deleted, delete its activity logs

            // ActivityLog → User (Many Logs created by One User)
            entity.HasOne(tal => tal.ChangedByUser)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(tal => tal.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);  // Don't allow deleting user if they have activity logs
        });

        // ===== SEED INITIAL DATA =====
        // password: Admin@12345
        var superAdmin = new Users
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Email = "admin@ticketing.com",
            FirstName = "System",
            LastName = "Administrator",
            PhoneNumber = "0714665512",
            PasswordHash = "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.",
            Role = UserRole.SuperAdmin,
            Status = UserStatus.Active,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),  
            UpdatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc), 
        };
        // ===== TEAMLEAD 1 =====
        var teamLead1 = new Users
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
            Email = "teamlead1@ticketing.com",
            FirstName = "John",
            LastName = "Manager",
            PhoneNumber = "0722000001",
            PasswordHash = "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.",
            Role = UserRole.TeamLead,
            Status = UserStatus.Active,
            IsActive = true,
            IsEmailVerified = true,
            InvitedByUserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
           CreatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),  
            UpdatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),
        };

        // ===== TECHNICIAN 1 =====
        // ===== TECHNICIAN 1 (Under TeamLead 1) =====
        var technician1 = new Users
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440010"),
            Email = "technician1@ticketing.com",
            FirstName = "Caleb",
            LastName = "Tech",
            PhoneNumber = "0722000010",
            PasswordHash = "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.",
            Role = UserRole.Technician,
            Status = UserStatus.Active,
            IsActive = true,
            IsEmailVerified = true,
            InvitedByUserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
            TeamLeadUserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
            CreatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),  
            UpdatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),
        };
        // ===== CUSTOMER 1 =====
        var customer1 = new Users
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440020"),
            Email = "customer1@ticketing.com",
            FirstName = "Frank",
            LastName = "Client",
            PhoneNumber = "0722000020",
            PasswordHash = "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.",
            Role = UserRole.Customer,
            Status = UserStatus.Active,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),  
            UpdatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),
        };
        
        // ===== SUPPORT (Optional) =====
        var support = new Users
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440030"),
            Email = "support@ticketing.com",
            FirstName = "Burton",
            LastName = "Support",
            PhoneNumber = "0722000030",
            PasswordHash = "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.",
            Role = UserRole.Support,
            Status = UserStatus.Active,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),  
            UpdatedAt = new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc),
        };

        modelBuilder.Entity<Users>().HasData(superAdmin, teamLead1, technician1, customer1, support);
    }
}