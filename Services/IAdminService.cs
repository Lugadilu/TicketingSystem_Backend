// File: Services/IAdminService.cs

using TicketingSystem.Api.Dtos.Admin;

namespace TicketingSystem.Api.Services;

public interface IAdminService
{
    /// Get admin dashboard statistics
    /// Returns counts of users by role, tickets by status, and performance metrics
    Task<AdminStatsDto> GetAdminStatsAsync();
}