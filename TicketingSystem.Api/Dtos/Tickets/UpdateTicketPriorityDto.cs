using System;
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Dtos.Tickets;

public class UpdateTicketPriorityDto
{
    [Required]
    public Priority Priority { get; set; }
}
