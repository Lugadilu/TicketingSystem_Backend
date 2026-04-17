using System;
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.DTOs.Tickets;

public class AssignTicketDto
{
    [Required(ErrorMessage = "User ID is required")]
    public Guid AssignedToUserId { get; set; }

    [MaxLength(255, ErrorMessage = "Reason cannot exceed 255 characters")]
    public string? Reason { get; set; }
}
