using System;
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Api.Dtos.Comments;

public class CreateCommentDto
{
    [Required(ErrorMessage = "Comment is required")]
    [MinLength(1, ErrorMessage = "Comment cannot be empty")]
    [MaxLength(5000, ErrorMessage = "Comment cannot exceed 5000 characters")]
    public string Comment { get; set; } = string.Empty;

    [Url(ErrorMessage = "Invalid URL format")]
    [MaxLength(500, ErrorMessage = "Attachment URL cannot exceed 500 characters")]
    public string? AttachmentUrl { get; set; }
}