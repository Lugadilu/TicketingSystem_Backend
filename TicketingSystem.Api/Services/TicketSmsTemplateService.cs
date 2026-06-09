using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface ITicketSmsTemplateService
{
    string BuildStatusMessage(TicketSmsNotification notification);
}

public sealed class TicketSmsTemplateService : ITicketSmsTemplateService
{
    public string BuildStatusMessage(TicketSmsNotification notification)
    {
        return notification.OldStatus is null
            ? BuildCreatedMessage(notification)
            : BuildChangedMessage(notification);
    }

    private static string BuildCreatedMessage(TicketSmsNotification notification)
    {
        return
            $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} has been created and is now Open. Please do not reply to this SMS.";
    }

    private static string BuildChangedMessage(TicketSmsNotification notification)
    {
        return notification.NewStatus switch
        {
            TicketStatus.InProgress =>
                $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} is now In Progress. Please do not reply to this SMS.",

            TicketStatus.Resolved =>
                $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} has been Resolved. Please log in to confirm or reopen it. Please do not reply to this SMS.",

            TicketStatus.Reopened =>
                $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} has been Reopened. Our team will continue working on it. Please do not reply to this SMS.",

            TicketStatus.Closed =>
                $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} has been Closed. Thank you. Please do not reply to this SMS.",

            _ =>
                $"Hi {notification.CustomerFirstName}, your ticket {notification.TicketNumber} changed from {notification.OldStatus} to {notification.NewStatus}. Please do not reply to this SMS."
        };
    }
}