// File: DTOs/Admin/AdminStatsDto.cs

namespace TicketingSystem.Api.Dtos.Admin
{
    public class AdminStatsDto
    {
        // User Statistics
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalTeamLeads { get; set; }
        public int TotalTechnicians { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalSupport { get; set; }

        // Ticket Statistics
        public int TotalTickets { get; set; }
        public int OpenTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int ClosedTickets { get; set; }

        // Performance Statistics
        public double AverageResolutionTimeHours { get; set; }
        
        // Metadata
        public DateTime LastUpdated { get; set; }
    }
}