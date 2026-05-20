using System;
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.DTOs.Tickets;

public class CreateTicketDto
{
    [Required(ErrorMessage = "Title is required")]
    [MinLength(3, ErrorMessage = "Title must be at least 3 characters")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    [MinLength(2, ErrorMessage = "Category must be at least 2 characters")]
    [MaxLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Priority is required")]
    public Priority Priority { get; set; } = Priority.Medium;
}
