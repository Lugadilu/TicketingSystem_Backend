using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketingSystem.Api.Models;
using TicketingSystem.Api.Services;
using Microsoft.AspNetCore.Authorization; 

namespace TicketingSystem.Api.Controllers;

/// <summary>
/// Configuration Controller - Provides non-sensitive configuration to frontend
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly GoogleAuthSettings _googleAuthSettings;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IOptions<GoogleAuthSettings> googleAuthSettings, ILogger<ConfigController> logger)
    {
        _googleAuthSettings = googleAuthSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/config/google-client-id
    /// Get Google Client ID (non-sensitive public information)
    /// Frontend uses this to initialize Google OAuth
    /// </summary>
    /// <returns>Google Client ID</returns>
    [HttpGet("google-client-id")]
    [AllowAnonymous]  //  This is public, non-sensitive information
    public IActionResult GetGoogleClientId()
    {
        try
        {
            if (string.IsNullOrEmpty(_googleAuthSettings.ClientId))
            {
                _logger.LogWarning("Google ClientId is not configured in appsettings.json");
                return BadRequest(new { error = "Google authentication is not configured" });
            }

            _logger.LogInformation("Returning Google ClientId to frontend");

            return Ok(new
            {
                clientId = _googleAuthSettings.ClientId,
                isConfigured = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Google ClientId");
            return StatusCode(500, new { error = "Failed to fetch configuration" });
        }
    }

    /// <summary>
    /// GET /api/config/auth-config
    /// Get all public authentication configuration
    /// </summary>
    [HttpGet("auth-config")]
    [AllowAnonymous]
    public IActionResult GetAuthConfig()
    {
        try
        {
            return Ok(new
            {
                googleAuth = new
                {
                    clientId = _googleAuthSettings.ClientId,
                    isConfigured = !string.IsNullOrEmpty(_googleAuthSettings.ClientId)
                },
                apiUrl = "api",  // Frontend can use relative URLs
                version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching auth config");
            return StatusCode(500, new { error = "Failed to fetch configuration" });
        }
    }
}
