using System;

namespace TicketingSystem.Api.Models;

public enum UserStatus
{
    Pending = 1,      // Invited, waiting to accept and set password
    Active = 2,       // Active user, can login
    Inactive = 3,     // Deactivated by admin
    Suspended = 4     // Suspended for violation
}