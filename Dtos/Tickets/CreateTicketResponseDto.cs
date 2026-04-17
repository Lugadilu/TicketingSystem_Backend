using System;

namespace TicketingSystem.Api.DTOs.Tickets;

public class CreateTicketResponseDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Message { get; set; } = "Ticket created successfully";
}
