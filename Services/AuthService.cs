using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Auth;
using TicketingSystem.Api.Models;
using Google.Apis.Auth;
using TicketingSystem.Api.Services;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Services;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterDto dto);
    Task<bool> VerifyOtpAsync(string email, string otp);
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto);
    Task<bool> ResendOtpAsync(string email);
    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto);
    Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto);
    Task<bool> ResendPasswordResetCodeAsync(string email);
    Task<LoginResponseDto> GoogleLoginAsync(string idToken);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IInvitationService _invitationService;
    private readonly IEmailService _emailService;
    
    //  FIX: Add this field declaration
    private readonly GoogleAuthSettings _googleAuth;


    public AuthService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IJwtTokenService jwtTokenService,
        IInvitationService invitationService,
        IEmailService emailService,
        IOptions<GoogleAuthSettings> googleAuthSettings) //  Fixed parameter name
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _invitationService = invitationService;
        _emailService = emailService;
        
        // ✅ FIX: Properly assign from googleAuthSettings parameter
        _googleAuth = googleAuthSettings.Value;
    }

    // REGISTER: Customer self-registration
    public async Task<RegisterResponseDto> RegisterAsync(RegisterDto dto)
    {
        // Check if email already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
            throw new InvalidOperationException("Email already registered");

        // Generate OTP
        var otp = _otpService.GenerateOtp();

        // Create user
        var user = new Users
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber ?? string.Empty,
            Role = UserRole.Customer,
            Status = UserStatus.Active,  // Customers are active immediately
            IsEmailVerified = false,
            EmailVerificationOTP = otp,
            OTPExpiresAt = _otpService.GetOtpExpiryTime(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Send OTP via email
        await _emailService.SendOtpAsync(user.Email, otp);

        return new RegisterResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Registration successful. Please verify your email with the OTP sent."
        };
    }

    // VERIFY OTP: Confirm email after registration
    public async Task<bool> VerifyOtpAsync(string email, string otp)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Check if OTP expired
        if (_otpService.IsOtpExpired(user.OTPExpiresAt))
            throw new InvalidOperationException("OTP has expired");

        // Check if OTP matches
        if (user.EmailVerificationOTP != otp)
            throw new InvalidOperationException("Invalid OTP");

        // Mark email as verified
        user.IsEmailVerified = true;
        user.EmailVerificationOTP = string.Empty;
        user.OTPExpiresAt = null;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return true;
    }

    //  GOOGLE LOGIN: Authenticate with Google OAuth
    public async Task<LoginResponseDto> GoogleLoginAsync(string idToken)
    {
        // Validate the Google ID token
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleAuth.ClientId } // Now _googleAuth is properly defined
            });

        var email = payload.Email;
        var firstName = payload.GivenName;
        var lastName = payload.FamilyName;

        // Check if user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            // Create new user from Google profile
            user = new Users
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                PasswordHash = "", // Google users don't need password
                Role = UserRole.Customer,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);
        var expiryTime = _jwtTokenService.GetTokenExpiryTime();

        return new LoginResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiryTime
        };
    }

    // LOGIN: Authenticate user with email and password
    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new InvalidOperationException("Invalid email or password");

        // Check if email verified
        if (!user.IsEmailVerified)
            throw new InvalidOperationException("Email not verified. Please verify your email first.");

        // Verify password
        if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            throw new InvalidOperationException("Invalid email or password");

        // Check if user is active
        if (!user.IsActive || user.Status != UserStatus.Active)
            throw new InvalidOperationException("User account is not active");

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);
        var expiryTime = _jwtTokenService.GetTokenExpiryTime();

        return new LoginResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiryTime
        };
    }

    // ACCEPT INVITATION: Invited users set password and activate account
    public async Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Check if invitation token matches
        if (user.InvitationToken != dto.InvitationToken)
            throw new InvalidOperationException("Invalid invitation token");

        // Check if invitation expired
        if (_invitationService.IsInvitationExpired(user.InvitationExpiresAt))
            throw new InvalidOperationException("Invitation has expired");

        // Set password and activate account
        user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
        user.Status = UserStatus.Active;
        user.InvitationToken = null;
        user.InvitationExpiresAt = null;
        user.InvitationAcceptedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return new AcceptInvitationResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Invitation accepted successfully. You can now login."
        };
    }

    // RESEND OTP: Send new OTP if expired
    public async Task<bool> ResendOtpAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.IsEmailVerified)
            throw new InvalidOperationException("Email already verified");

        // Generate new OTP
        var otp = _otpService.GenerateOtp();
        user.EmailVerificationOTP = otp;
        user.OTPExpiresAt = _otpService.GetOtpExpiryTime();

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Send OTP via email
        await _emailService.SendOtpAsync(user.Email, otp);

        return true;
    }

    // FORGOT PASSWORD: Generate 6-digit code and send email
    public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        // Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Generate 6-digit reset code (same as OTP)
        var resetCode = _otpService.GenerateOtp();
        var resetExpiryTime = _otpService.GetOtpExpiryTime();

        // Save code to user
        user.PasswordResetToken = resetCode;
        user.PasswordResetExpiresAt = resetExpiryTime;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Send reset code via email
        await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode, $"{user.FirstName} {user.LastName}");

        return new ForgotPasswordResponseDto
        {
            Email = user.Email,
            Message = "Password reset code sent to your email. It expires in 10 minutes."
        };
    }

    // RESET PASSWORD: Validate 6-digit code and update password
    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto)
    {
        // Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Check if reset code matches
        if (user.PasswordResetToken != dto.ResetCode)
            throw new InvalidOperationException("Invalid reset code");

        // Check if code expired
        if (_otpService.IsOtpExpired(user.PasswordResetExpiresAt))
            throw new InvalidOperationException("Reset code has expired");

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return new ResetPasswordResponseDto
        {
            Email = user.Email,
            Message = "Password reset successfully. You can now login with your new password."
        };
    }

    // RESEND PASSWORD RESET CODE: Send new code if expired
    public async Task<bool> ResendPasswordResetCodeAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Generate new reset code
        var resetCode = _otpService.GenerateOtp();
        user.PasswordResetToken = resetCode;
        user.PasswordResetExpiresAt = _otpService.GetOtpExpiryTime();

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Send reset code via email
        await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode, $"{user.FirstName} {user.LastName}");

        return true;
    }
}





// using System;
// using System.Threading.Tasks;
// using Microsoft.EntityFrameworkCore;
// using TicketingSystem.Api.Data;
// using TicketingSystem.Api.Dtos.Auth;
// using TicketingSystem.Api.Models;
// using Google.Apis.Auth;
// using TicketingSystem.Api.Services;
// using Microsoft.Extensions.Options;

// namespace TicketingSystem.Api.Services;

// public interface IAuthService
// {
//     Task<RegisterResponseDto> RegisterAsync(RegisterDto dto);
//     Task<bool> VerifyOtpAsync(string email, string otp);
//     Task<LoginResponseDto> LoginAsync(LoginDto dto);
//     Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto);
//     Task<bool> ResendOtpAsync(string email);
//     Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto);
//     Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto);
//     Task<bool> ResendPasswordResetCodeAsync(string email);

//     Task<LoginResponseDto> GoogleLoginAsync(string idToken);
// }

// public class AuthService : IAuthService
// {
//     private readonly AppDbContext _context;
//     private readonly IPasswordHasher _passwordHasher;
//     private readonly IOtpService _otpService;
//     private readonly IJwtTokenService _jwtTokenService;
//     private readonly IInvitationService _invitationService;
//     private readonly IEmailService _emailService;
//     private readonly GoogleAuthSettings _googleAuth;


//     public AuthService(
//         AppDbContext context,
//         IPasswordHasher passwordHasher,
//         IOtpService otpService,
//         IJwtTokenService jwtTokenService,
//         IInvitationService invitationService,
//         IEmailService emailService,
//         IOptions<GoogleAuthSettings> googleAuthSettings)
//     {
//         _context = context;
//         _passwordHasher = passwordHasher;
//         _otpService = otpService;
//         _jwtTokenService = jwtTokenService;
//         _invitationService = invitationService;
//         _emailService = emailService;
//          _googleAuth = googleAuth.Value;
//     }

//     // REGISTER: Customer self-registration
//     public async Task<RegisterResponseDto> RegisterAsync(RegisterDto dto)
//     {
//         // Check if email already exists
//         var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
//         if (existingUser != null)
//             throw new InvalidOperationException("Email already registered");

//         // Generate OTP
//         var otp = _otpService.GenerateOtp();

//         // Create user
//         var user = new Users
//         {
//             Id = Guid.NewGuid(),
//             Email = dto.Email,
//             PasswordHash = _passwordHasher.HashPassword(dto.Password),
//             FirstName = dto.FirstName,
//             LastName = dto.LastName,
//             PhoneNumber = dto.PhoneNumber ?? string.Empty,
//             Role = UserRole.Customer,
//             Status = UserStatus.Active,  // Customers are active immediately
//             IsEmailVerified = false,
//             EmailVerificationOTP = otp,
//             OTPExpiresAt = _otpService.GetOtpExpiryTime(),
//             IsActive = true,
//             CreatedAt = DateTime.UtcNow,
//             UpdatedAt = DateTime.UtcNow
//         };

//         await _context.Users.AddAsync(user);
//         await _context.SaveChangesAsync();

//         // Send OTP via email
//         await _emailService.SendOtpAsync(user.Email, otp);

//         return new RegisterResponseDto
//         {
//             UserId = user.Id,
//             Email = user.Email,
//             FirstName = user.FirstName,
//             LastName = user.LastName,
//             Message = "Registration successful. Please verify your email with the OTP sent."
//         };
//     }

//     // VERIFY OTP: Confirm email after registration
//     public async Task<bool> VerifyOtpAsync(string email, string otp)
//     {
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         // Check if OTP expired
//         if (_otpService.IsOtpExpired(user.OTPExpiresAt))
//             throw new InvalidOperationException("OTP has expired");

//         // Check if OTP matches
//         if (user.EmailVerificationOTP != otp)
//             throw new InvalidOperationException("Invalid OTP");

//         // Mark email as verified
//         user.IsEmailVerified = true;
//         user.EmailVerificationOTP = string.Empty;
//         user.OTPExpiresAt = null;

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         return true;
//     }

//     // LOGIN: Authenticate user
//     // login with google account
//         public async Task<LoginResponseDto> GoogleLoginAsync(string idToken)
//     {
//         //var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);

//     var payload = await GoogleJsonWebSignature.ValidateAsync(
//     idToken,
//         new GoogleJsonWebSignature.ValidationSettings
//         {
//             Audience = new[] { _googleAuth.ClientId }
//         });

//         var email = payload.Email;
//         var firstName = payload.GivenName;
//         var lastName = payload.FamilyName;

//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

//         if (user == null)
//         {
//             user = new Users
//             {
//                 Id = Guid.NewGuid(),
//                 Email = email,
//                 FirstName = firstName,
//                 LastName = lastName,
//                 PasswordHash = "", // Google users don't need password
//                 Role = UserRole.Customer,
//                 Status = UserStatus.Active,
//                 IsEmailVerified = true,
//                 IsActive = true,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };

//             await _context.Users.AddAsync(user);
//             await _context.SaveChangesAsync();
//         }

//         var token = _jwtTokenService.GenerateToken(user);
//         var expiryTime = _jwtTokenService.GetTokenExpiryTime();

//         return new LoginResponseDto
//         {
//             UserId = user.Id,
//             Email = user.Email,
//             FirstName = user.FirstName,
//             LastName = user.LastName,
//             Role = user.Role,
//             Token = token,
//             ExpiresAt = expiryTime
//         };
//     }

//     // LOGIN: Authenticate user
//     public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
//     {
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
//         if (user == null)
//             throw new InvalidOperationException("Invalid email or password");

//         // Check if email verified
//         if (!user.IsEmailVerified)
//             throw new InvalidOperationException("Email not verified. Please verify your email first.");

//         // Verify password
//         if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
//             throw new InvalidOperationException("Invalid email or password");

//         // Check if user is active
//         if (!user.IsActive || user.Status != UserStatus.Active)
//             throw new InvalidOperationException("User account is not active");

//         // Generate JWT token
//         var token = _jwtTokenService.GenerateToken(user);
//         var expiryTime = _jwtTokenService.GetTokenExpiryTime();

//         return new LoginResponseDto
//         {
//             UserId = user.Id,
//             Email = user.Email,
//             FirstName = user.FirstName,
//             LastName = user.LastName,
//             Role = user.Role,
//             Token = token,
//             ExpiresAt = expiryTime
//         };
//     }

//     // ACCEPT INVITATION: Invited users set password and activate account
//     public async Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto)
//     {
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         // Check if invitation token matches
//         if (user.InvitationToken != dto.InvitationToken)
//             throw new InvalidOperationException("Invalid invitation token");

//         // Check if invitation expired
//         if (_invitationService.IsInvitationExpired(user.InvitationExpiresAt))
//             throw new InvalidOperationException("Invitation has expired");

//         // Set password and activate account
//         user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
//         user.Status = UserStatus.Active;
//         user.InvitationToken = null;
//         user.InvitationExpiresAt = null;
//         user.InvitationAcceptedAt = DateTime.UtcNow;
//         user.UpdatedAt = DateTime.UtcNow;

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         return new AcceptInvitationResponseDto
//         {
//             UserId = user.Id,
//             Email = user.Email,
//             FirstName = user.FirstName,
//             LastName = user.LastName,
//             Message = "Invitation accepted successfully. You can now login."
//         };
//     }

//     // RESEND OTP: Send new OTP if expired
//     public async Task<bool> ResendOtpAsync(string email)
//     {
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         if (user.IsEmailVerified)
//             throw new InvalidOperationException("Email already verified");

//         // Generate new OTP
//         var otp = _otpService.GenerateOtp();
//         user.EmailVerificationOTP = otp;
//         user.OTPExpiresAt = _otpService.GetOtpExpiryTime();

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         // Send OTP via email
//         await _emailService.SendOtpAsync(user.Email, otp);

//         return true;
//     }

//     // FORGOT PASSWORD: Generate 6-digit code and send email
//     public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto)
//     {
//         // Find user by email
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         // Generate 6-digit reset code (same as OTP)
//         var resetCode = _otpService.GenerateOtp();
//         var resetExpiryTime = _otpService.GetOtpExpiryTime();

//         // Save code to user
//         user.PasswordResetToken = resetCode;
//         user.PasswordResetExpiresAt = resetExpiryTime;
//         user.UpdatedAt = DateTime.UtcNow;

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         // Send reset code via email
//         await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode, $"{user.FirstName} {user.LastName}");

//         return new ForgotPasswordResponseDto
//         {
//             Email = user.Email,
//             Message = "Password reset code sent to your email. It expires in 10 minutes."
//         };
//     }

//     // RESET PASSWORD: Validate 6-digit code and update password
//     public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto)
//     {
//         // Find user by email
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         // Check if reset code matches
//         if (user.PasswordResetToken != dto.ResetCode)
//             throw new InvalidOperationException("Invalid reset code");

//         // Check if code expired
//         if (_otpService.IsOtpExpired(user.PasswordResetExpiresAt))
//             throw new InvalidOperationException("Reset code has expired");

//         // Update password
//         user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
//         user.PasswordResetToken = null;
//         user.PasswordResetExpiresAt = null;
//         user.UpdatedAt = DateTime.UtcNow;

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         return new ResetPasswordResponseDto
//         {
//             Email = user.Email,
//             Message = "Password reset successfully. You can now login with your new password."
//         };
//     }

//     // RESEND PASSWORD RESET CODE: Send new code if expired
//     public async Task<bool> ResendPasswordResetCodeAsync(string email)
//     {
//         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
//         if (user == null)
//             throw new InvalidOperationException("User not found");

//         // Generate new reset code
//         var resetCode = _otpService.GenerateOtp();
//         user.PasswordResetToken = resetCode;
//         user.PasswordResetExpiresAt = _otpService.GetOtpExpiryTime();

//         _context.Users.Update(user);
//         await _context.SaveChangesAsync();

//         // Send reset code via email
//         await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode, $"{user.FirstName} {user.LastName}");

//         return true;
//     }
// }


// /////with reset link for forgot password and token validation for reset password

// // using System;
// // using System.Threading.Tasks;
// // using Microsoft.EntityFrameworkCore;
// // using TicketingSystem.Api.Data;
// // using TicketingSystem.Api.Dtos.Auth;
// // using TicketingSystem.Api.Models;

// // namespace TicketingSystem.Api.Services;

// // public interface IAuthService
// // {
// //     Task<RegisterResponseDto> RegisterAsync(RegisterDto dto);
// //     Task<bool> VerifyOtpAsync(string email, string otp);
// //     Task<LoginResponseDto> LoginAsync(LoginDto dto);
// //     Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto);
// //     Task<bool> ResendOtpAsync(string email);
// //     Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto);
// //     Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto);
// // }

// // public class AuthService : IAuthService
// // {
// //     private readonly AppDbContext _context;
// //     private readonly IPasswordHasher _passwordHasher;
// //     private readonly IOtpService _otpService;
// //     private readonly IJwtTokenService _jwtTokenService;
// //     private readonly IInvitationService _invitationService;
// //     private readonly IEmailService _emailService;

// //     public AuthService(
// //         AppDbContext context,
// //         IPasswordHasher passwordHasher,
// //         IOtpService otpService,
// //         IJwtTokenService jwtTokenService,
// //         IInvitationService invitationService,
// //         IEmailService emailService)
// //     {
// //         _context = context;
// //         _passwordHasher = passwordHasher;
// //         _otpService = otpService;
// //         _jwtTokenService = jwtTokenService;
// //         _invitationService = invitationService;
// //         _emailService = emailService;
// //     }

// //     // REGISTER: Customer self-registration
// //     public async Task<RegisterResponseDto> RegisterAsync(RegisterDto dto)
// //     {
// //         // Check if email already exists
// //         var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
// //         if (existingUser != null)
// //             throw new InvalidOperationException("Email already registered");

// //         // Generate OTP
// //         var otp = _otpService.GenerateOtp();

// //         // Create user
// //         var user = new Users
// //         {
// //             Id = Guid.NewGuid(),
// //             Email = dto.Email,
// //             PasswordHash = _passwordHasher.HashPassword(dto.Password),
// //             FirstName = dto.FirstName,
// //             LastName = dto.LastName,
// //             PhoneNumber = dto.PhoneNumber ?? string.Empty,
// //             Role = UserRole.Customer,
// //             Status = UserStatus.Active,  // Customers are active immediately
// //             IsEmailVerified = false,
// //             EmailVerificationOTP = otp,
// //             OTPExpiresAt = _otpService.GetOtpExpiryTime(),
// //             IsActive = true,
// //             CreatedAt = DateTime.UtcNow,
// //             UpdatedAt = DateTime.UtcNow
// //         };

// //         await _context.Users.AddAsync(user);
// //         await _context.SaveChangesAsync();

// //         // Send OTP via email
// //         await _emailService.SendOtpAsync(user.Email, otp);

// //         return new RegisterResponseDto
// //         {
// //             UserId = user.Id,
// //             Email = user.Email,
// //             FirstName = user.FirstName,
// //             LastName = user.LastName,
// //             Message = "Registration successful. Please verify your email with the OTP sent."
// //         };
// //     }

// //     // VERIFY OTP: Confirm email after registration
// //     public async Task<bool> VerifyOtpAsync(string email, string otp)
// //     {
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
// //         if (user == null)
// //             throw new InvalidOperationException("User not found");

// //         // Check if OTP expired
// //         if (_otpService.IsOtpExpired(user.OTPExpiresAt))
// //             throw new InvalidOperationException("OTP has expired");

// //         // Check if OTP matches
// //         if (user.EmailVerificationOTP != otp)
// //             throw new InvalidOperationException("Invalid OTP");

// //         // Mark email as verified
// //         user.IsEmailVerified = true;
// //         user.EmailVerificationOTP = string.Empty;
// //         user.OTPExpiresAt = null;

// //         _context.Users.Update(user);
// //         await _context.SaveChangesAsync();

// //         return true;
// //     }

// //     // LOGIN: Authenticate user
// //     public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
// //     {
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
// //         if (user == null)
// //             throw new InvalidOperationException("Invalid email or password");

// //         // Check if email verified
// //         if (!user.IsEmailVerified)
// //             throw new InvalidOperationException("Email not verified. Please verify your email first.");

// //         // Verify password
// //         if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
// //             throw new InvalidOperationException("Invalid email or password");

// //         // Check if user is active
// //         if (!user.IsActive || user.Status != UserStatus.Active)
// //             throw new InvalidOperationException("User account is not active");

// //         // Generate JWT token
// //         var token = _jwtTokenService.GenerateToken(user);
// //         var expiryTime = _jwtTokenService.GetTokenExpiryTime();

// //         return new LoginResponseDto
// //         {
// //             UserId = user.Id,
// //             Email = user.Email,
// //             FirstName = user.FirstName,
// //             LastName = user.LastName,
// //             Role = user.Role,
// //             Token = token,
// //             ExpiresAt = expiryTime
// //         };
// //     }

// //     // ACCEPT INVITATION: Invited users set password and activate account
// //     public async Task<AcceptInvitationResponseDto> AcceptInvitationAsync(AcceptInvitationDto dto)
// //     {
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
// //         if (user == null)
// //             throw new InvalidOperationException("User not found");

// //         // Check if invitation token matches
// //         if (user.InvitationToken != dto.InvitationToken)
// //             throw new InvalidOperationException("Invalid invitation token");

// //         // Check if invitation expired
// //         if (_invitationService.IsInvitationExpired(user.InvitationExpiresAt))
// //             throw new InvalidOperationException("Invitation has expired");

// //         // Set password and activate account
// //         user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
// //         user.Status = UserStatus.Active;
// //         user.InvitationToken = null;
// //         user.InvitationExpiresAt = null;
// //         user.InvitationAcceptedAt = DateTime.UtcNow;
// //         user.UpdatedAt = DateTime.UtcNow;

// //         _context.Users.Update(user);
// //         await _context.SaveChangesAsync();

// //         return new AcceptInvitationResponseDto
// //         {
// //             UserId = user.Id,
// //             Email = user.Email,
// //             FirstName = user.FirstName,
// //             LastName = user.LastName,
// //             Message = "Invitation accepted successfully. You can now login."
// //         };
// //     }

// //     // RESEND OTP: Send new OTP if expired
// //     public async Task<bool> ResendOtpAsync(string email)
// //     {
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
// //         if (user == null)
// //             throw new InvalidOperationException("User not found");

// //         if (user.IsEmailVerified)
// //             throw new InvalidOperationException("Email already verified");

// //         // Generate new OTP
// //         var otp = _otpService.GenerateOtp();
// //         user.EmailVerificationOTP = otp;
// //         user.OTPExpiresAt = _otpService.GetOtpExpiryTime();

// //         _context.Users.Update(user);
// //         await _context.SaveChangesAsync();

// //         // Send OTP via email
// //         await _emailService.SendOtpAsync(user.Email, otp);

// //         return true;
// //     }

// //     // FORGOT PASSWORD: Generate reset token and send email
// //     public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto dto)
// //     {
// //         // Find user by email
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
// //         if (user == null)
// //             throw new InvalidOperationException("User not found");

// //         // Generate reset token
// //         var resetToken = _invitationService.GenerateInvitationToken();
// //         var resetExpiryTime = _invitationService.GetInvitationExpiryTime();

// //         // Save token to user
// //         user.PasswordResetToken = resetToken;
// //         user.PasswordResetExpiresAt = resetExpiryTime;
// //         user.UpdatedAt = DateTime.UtcNow;

// //         _context.Users.Update(user);
// //         await _context.SaveChangesAsync();

// //         // Send reset link via email
// //         var resetLink = $"https://yourfrontend.com/reset-password?email={dto.Email}&token={resetToken}";
// //         await _emailService.SendPasswordResetAsync(user.Email, resetLink, $"{user.FirstName} {user.LastName}");

// //         return new ForgotPasswordResponseDto
// //         {
// //             Email = user.Email,
// //             Message = "Password reset link sent to your email. It expires in 7 days."
// //         };
// //     }

// //     // RESET PASSWORD: Validate token and update password
// //     public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordDto dto)
// //     {
// //         // Find user by email
// //         var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
// //         if (user == null)
// //             throw new InvalidOperationException("User not found");

// //         // Check if reset token matches
// //         if (user.PasswordResetToken != dto.ResetToken)
// //             throw new InvalidOperationException("Invalid reset token");

// //         // Check if token expired
// //         if (_invitationService.IsInvitationExpired(user.PasswordResetExpiresAt))
// //             throw new InvalidOperationException("Reset token has expired");

// //         // Update password
// //         user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
// //         user.PasswordResetToken = null;
// //         user.PasswordResetExpiresAt = null;
// //         user.UpdatedAt = DateTime.UtcNow;

// //         _context.Users.Update(user);
// //         await _context.SaveChangesAsync();

// //         return new ResetPasswordResponseDto
// //         {
// //             Email = user.Email,
// //             Message = "Password reset successfully. You can now login with your new password."
// //         };
// //     }
// // }