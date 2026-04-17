// File: Controllers/AdminController.cs - WITH DEBUGGING

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Api.Dtos.Admin;
using TicketingSystem.Api.Services;
using System.Security.Claims;

namespace TicketingSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        /// <summary>
        /// Get admin dashboard statistics
        /// Requires: Admin role (role = 1)
        /// Returns: User counts by role, ticket counts by status, average resolution time
        /// </summary>
        [HttpGet("stats")]
        [Authorize] 
        public async Task<IActionResult> GetAdminStats()
        {
            try
            {
                _logger.LogInformation("GetAdminStats endpoint called");

                // DEBUG: Log all claims
                var allClaims = User.Claims.ToList();
                _logger.LogInformation($"Total claims: {allClaims.Count}");
                
                foreach (var claim in allClaims)
                {
                    _logger.LogInformation($"Claim - Type: {claim.Type}, Value: {claim.Value}");
                }

                // Find role claim
                var roleClaim = User.FindFirst(ClaimTypes.Role);
                if (roleClaim != null)
                {
                    _logger.LogInformation($"Role claim found: Type='{roleClaim.Type}', Value='{roleClaim.Value}'");
                }
                else
                {
                    _logger.LogWarning("No role claim found!");
                }

                var stats = await _adminService.GetAdminStatsAsync();

                return Ok(new
                {
                    success = true,
                    message = "Admin stats retrieved successfully",
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAdminStats");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve admin stats",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Debug endpoint - shows all claims in your JWT token
        /// Remove this in production!
        /// </summary>
        [HttpGet("debug-claims")]
        [Authorize]
        public IActionResult DebugClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            
            _logger.LogInformation($"Debug endpoint called. Claims count: {claims.Count}");
            foreach (var claim in claims)
            {
                _logger.LogInformation($"  {claim.Type} = {claim.Value}");
            }

            return Ok(new
            {
                success = true,
                message = "All claims from your JWT token",
                claimsCount = claims.Count,
                claims = claims
            });
        }
    }
}



// // File: Controllers/AdminController.cs

// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using TicketingSystem.Api.Dtos.Admin;
// using TicketingSystem.Api.Services;

// namespace TicketingSystem.Api.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     [Authorize]
//     public class AdminController : ControllerBase
//     {
//         private readonly IAdminService _adminService;
//         private readonly ILogger<AdminController> _logger;

//         public AdminController(IAdminService adminService, ILogger<AdminController> logger)
//         {
//             _adminService = adminService;
//             _logger = logger;
//         }

//         /// Get admin dashboard statistics
//         /// Requires: Admin role (role = 1)
//         /// Returns: User counts by role, ticket counts by status, average resolution time
//         [HttpGet("stats")]
//         [Authorize(Roles = "1")] // Only Super Admin (role 1)
//         public async Task<IActionResult> GetAdminStats()
//         {
//             try
//             {
//                 _logger.LogInformation("GetAdminStats endpoint called");

//                 var stats = await _adminService.GetAdminStatsAsync();

//                 return Ok(new
//                 {
//                     success = true,
//                     message = "Admin stats retrieved successfully",
//                     data = stats
//                 });
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error in GetAdminStats");

//                 return StatusCode(500, new
//                 {
//                     success = false,
//                     message = "Failed to retrieve admin stats",
//                     error = ex.Message
//                 });
//             }
//         }
//     }
// }




// // // File: Controllers/AdminController.cs

// // using Microsoft.AspNetCore.Mvc;
// // using TicketingSystem.Api.Dtos.Common;
// // using TicketingSystem.Api.Services;

// // namespace TicketingSystem.Api.Controllers;

// // [ApiController]
// // [Route("api/[controller]")]
// // public class AdminController : ControllerBase
// // {
// //     private readonly IAdminService _adminService;

// //     public AdminController(IAdminService adminService)
// //     {
// //         _adminService = adminService;
// //     }
// //     /// GET /api/admin/stats
// //     /// Get admin dashboard statistics
// //     /// 
// //     /// Returns:
// //     /// - User counts by role
// //     /// - Ticket counts by status
// //     /// - Average resolution time
// //     /// 
// //     /// Requires: Admin authentication

// //     [HttpGet("stats")]
// //     public async Task<ActionResult<ApiResponseDto<object>>> GetStats()
// //     {
// //         try
// //         {
// //             // Fetch stats from service
// //             var stats = await _adminService.GetAdminStatsAsync();

// //             // Return wrapped response
// //             return Ok(new ApiResponseDto<object>
// //             {
// //                 Success = true,
// //                 Message = "Admin stats retrieved successfully",
// //                 Data = stats,
// //                 Errors = new List<string>()
// //             });
// //         }
// //         catch (Exception ex)
// //         {
// //             return StatusCode(500, new ApiResponseDto<object>
// //             {
// //                 Success = false,
// //                 Message = "Error retrieving admin stats",
// //                 Errors = new List<string> { ex.Message }
// //             });
// //         }
// //     }
// // }