using System;

namespace TicketingSystem.Api.Models;

public enum TicketStatus
{
    Open = 1,         // Just created, waiting for support
    InProgress = 2,   // Someone is working on it
    Resolved = 3,     // Fixed, but not closed yet
    Reopened = 4,     // Customer said it's not fixed
    Closed = 5        // All done
}
