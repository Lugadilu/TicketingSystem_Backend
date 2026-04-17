using System;
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.DTOs.Tickets;

public class UpdateTicketDto
{
    [MinLength(3, ErrorMessage = "Title must be at least 3 characters")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string? Title { get; set; }

    [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
    public string? Description { get; set; }

    [MinLength(2, ErrorMessage = "Category must be at least 2 characters")]
    [MaxLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    public string? Category { get; set; }

    public Priority? Priority { get; set; }
}
