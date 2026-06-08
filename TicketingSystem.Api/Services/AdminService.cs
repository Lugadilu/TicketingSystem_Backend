// File: Services/AdminService.cs - FIXED VERSION

//using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Admin;
using TicketingSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace TicketingSystem.Api.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _context;

    public AdminService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Calculate admin dashboard statistics
    /// 
    /// Process:
    /// 1. Fetch all users and tickets from database
    /// 2. Count users by role (SuperAdmin, TeamLead, Technician, Customer, Support)
    /// 3. Count tickets by status (Open, InProgress, Resolved, Closed)
    /// 4. Calculate average resolution time for closed tickets
    /// 5. Return compiled statistics
    /// </summary>
    public async Task<AdminStatsDto> GetAdminStatsAsync()
    {
        // ===== STEP 1: Fetch all data =====
        var users = await _context.Users.ToListAsync();
        var tickets = await _context.Tickets.ToListAsync();

        // ===== STEP 2: Count users by role =====
        var totalAdmins = users.Count(u => u.Role == UserRole.SuperAdmin);
        var totalTeamLeads = users.Count(u => u.Role == UserRole.TeamLead);
        var totalTechnicians = users.Count(u => u.Role == UserRole.Technician);
        var totalCustomers = users.Count(u => u.Role == UserRole.Customer);
        var totalSupport = users.Count(u => u.Role == UserRole.Support);

        // ===== STEP 3: Count tickets by status =====
        // TicketStatus enum: Open=1, InProgress=2, Resolved=3, Closed=4
        var openTickets = tickets.Count(t => t.Status == TicketStatus.Open);
        var inProgressTickets = tickets.Count(t => t.Status == TicketStatus.InProgress);
        var resolvedTickets = tickets.Count(t => t.Status == TicketStatus.Resolved);
        var closedTickets = tickets.Count(t => t.Status == TicketStatus.Closed);

        // ===== STEP 4: Calculate average resolution time =====
        // Only consider tickets that have been closed and have a ClosedAt time
        // Resolution time = ClosedAt - CreatedAt
        var closedTicketsWithTime = tickets
            .Where(t => t.ClosedAt.HasValue)
            .ToList();

        double avgResolutionTime = 0;
        if (closedTicketsWithTime.Any())
        {
            // Calculate total hours for all closed tickets
            //var totalHours = closedTicketsWithTime
                //.Sum(t => (t.ClosedAt.Value - t.CreatedAt).TotalHours);
                var totalHours = closedTicketsWithTime
                .Sum(t => (t.ClosedAt!.Value - t.CreatedAt).TotalHours);
                
            
            // Divide by number of closed tickets
            avgResolutionTime = totalHours / closedTicketsWithTime.Count;
        }

        // ===== STEP 5: Return compiled statistics =====
        return new AdminStatsDto
        {
            // User Statistics
            TotalUsers = users.Count,
            TotalAdmins = totalAdmins,
            TotalTeamLeads = totalTeamLeads,
            TotalTechnicians = totalTechnicians,
            TotalCustomers = totalCustomers,
            TotalSupport = totalSupport,

            // Ticket Statistics
            TotalTickets = tickets.Count,
            OpenTickets = openTickets,
            InProgressTickets = inProgressTickets,
            ResolvedTickets = resolvedTickets,
            ClosedTickets = closedTickets,

            // Performance Metrics
            // Average time from creation to closure (in hours)
            AverageResolutionTimeHours = Math.Round(avgResolutionTime, 2),
            
            // Metadata
            LastUpdated = DateTime.UtcNow
        };
    }
}