using System;
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.DTOs.Tickets;

public class ChangeTicketStatusDto
{
    [Required(ErrorMessage = "Status is required")]
    public TicketStatus Status { get; set; }

    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }
}
