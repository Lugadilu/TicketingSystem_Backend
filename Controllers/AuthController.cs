using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Api.Dtos.Auth;
using TicketingSystem.Api.Dtos.Common;
using TicketingSystem.Api.DTOs.Auth;
using TicketingSystem.Api.Models;
using TicketingSystem.Api.Services;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// POST /api/auth/register
    /// Customer self-registration with email verification
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponseDto<RegisterResponseDto>>> Register([FromBody] RegisterDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<RegisterResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.RegisterAsync(dto);

            return Ok(new ApiResponseDto<RegisterResponseDto>
            {
                Success = true,
                Message = "Registration successful. OTP sent to email.",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<RegisterResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<RegisterResponseDto>
            {
                Success = false,
                Message = "An error occurred during registration",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/verify-otp
    /// Verify email with OTP code
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponseDto<VerifyOtpResponseDto>>> VerifyOtp([FromBody] VerifyOtpDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<VerifyOtpResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.VerifyOtpAsync(dto.Email, dto.Otp);

            return Ok(new ApiResponseDto<VerifyOtpResponseDto>
            {
                Success = true,
                Message = "Email verified successfully",
                Data = new VerifyOtpResponseDto
                {
                    IsVerified = result,
                    Message = "You can now login"
                },
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<VerifyOtpResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<VerifyOtpResponseDto>
            {
                Success = false,
                Message = "An error occurred during verification",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/resend-otp
    /// Resend OTP if expired or not received
    /// </summary>
    [HttpPost("resend-otp")]
    public async Task<ActionResult<ApiResponseDto<string>>> ResendOtp([FromBody] ResendOtpDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<string>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.ResendOtpAsync(dto.Email);

            return Ok(new ApiResponseDto<string>
            {
                Success = true,
                Message = "OTP sent to email",
                Data = "Check your email for the new OTP",
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<string>
            {
                Success = false,
                Message = "An error occurred",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/login
    /// User login - returns JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponseDto<LoginResponseDto>>> Login([FromBody] LoginDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<LoginResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.LoginAsync(dto);

            return Ok(new ApiResponseDto<LoginResponseDto>
            {
                Success = true,
                Message = "Login successful",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<LoginResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<LoginResponseDto>
            {
                Success = false,
                Message = "An error occurred during login",
                Errors = new List<string> { ex.Message }
            });
        }
    }
        // LOGIN: Authenticate user with google account
        [HttpPost("google-login")]
    public async Task<ActionResult<ApiResponseDto<LoginResponseDto>>> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        try
        {
            var result = await _authService.GoogleLoginAsync(dto.IdToken);

            return Ok(new ApiResponseDto<LoginResponseDto>
            {
                Success = true,
                Message = "Google login successful",
                Data = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponseDto<LoginResponseDto>
            {
                Success = false,
                Message = ex.Message
            });
        }
    }
    /// <summary>
    /// POST /api/auth/accept-invitation
    /// Invited users accept invitation and set password
    /// </summary>
    [HttpPost("accept-invitation")]
    public async Task<ActionResult<ApiResponseDto<AcceptInvitationResponseDto>>> AcceptInvitation([FromBody] AcceptInvitationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<AcceptInvitationResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.AcceptInvitationAsync(dto);

            return Ok(new ApiResponseDto<AcceptInvitationResponseDto>
            {
                Success = true,
                Message = "Invitation accepted successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<AcceptInvitationResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<AcceptInvitationResponseDto>
            {
                Success = false,
                Message = "An error occurred",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/forgot-password
    /// User requests password reset code
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponseDto<ForgotPasswordResponseDto>>> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<ForgotPasswordResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.ForgotPasswordAsync(dto);

            return Ok(new ApiResponseDto<ForgotPasswordResponseDto>
            {
                Success = true,
                Message = "Password reset code sent to email",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<ForgotPasswordResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<ForgotPasswordResponseDto>
            {
                Success = false,
                Message = "An error occurred",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/resend-password-reset-code
    /// Resend password reset code if expired
    /// </summary>
    [HttpPost("resend-password-reset-code")]
    public async Task<ActionResult<ApiResponseDto<string>>> ResendPasswordResetCode([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<string>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.ResendPasswordResetCodeAsync(dto.Email);

            return Ok(new ApiResponseDto<string>
            {
                Success = true,
                Message = "Password reset code sent to email",
                Data = "Check your email for the new reset code",
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<string>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<string>
            {
                Success = false,
                Message = "An error occurred",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// POST /api/auth/reset-password
    /// User resets password with 6-digit code
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponseDto<ResetPasswordResponseDto>>> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponseDto<ResetPasswordResponseDto>
                {
                    Success = false,
                    Message = "Invalid input",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });

            var result = await _authService.ResetPasswordAsync(dto);

            return Ok(new ApiResponseDto<ResetPasswordResponseDto>
            {
                Success = true,
                Message = "Password reset successfully",
                Data = result,
                Errors = new List<string>()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponseDto<ResetPasswordResponseDto>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto<ResetPasswordResponseDto>
            {
                Success = false,
                Message = "An error occurred",
                Errors = new List<string> { ex.Message }
            });
        }
    }
}