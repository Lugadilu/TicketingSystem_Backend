using System;
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.Dtos.Auth;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reset code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Reset code must be 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Reset code must contain only numbers")]
    public string ResetCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [MaxLength(255, ErrorMessage = "Password cannot exceed 255 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).*$",
        ErrorMessage = "Password must contain uppercase, lowercase, number, and special character")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}