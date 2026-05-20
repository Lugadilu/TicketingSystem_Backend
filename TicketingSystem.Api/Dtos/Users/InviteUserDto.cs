using System;
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;


namespace TicketingSystem.Api.Dtos.Users;
/// SuperAdmin uses this to invite TeamLeads
/// TeamLead uses this to invite Technicians
public class InviteUserDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    public UserRole Role { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}