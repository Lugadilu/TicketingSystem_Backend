using System;

namespace TicketingSystem.Api.Models;

public enum UserRole
{
    SuperAdmin = 1,   // Creates everything (TeamLeads, manages system)
    TeamLead = 2,     // Manages team, assigns work, closes tickets
    Support = 3,      // Reviews and prioritizes tickets , assigns(no implementation)
    Technician = 4,   // Implements solutions (technical work)
    Customer = 6      // Creates tickets
}