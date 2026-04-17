using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.Dtos.Admin;


/// For SuperAdmin to manually create Customer accounts.
/// Usually Customers self-register via /auth/register.

public class CreateCustomerDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [MinLength(2)]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MinLength(2)]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number")]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}