namespace TicketingSystem.Api.Services;

public interface ITicketSmsNotificationService
{
    Task SendTicketStatusNotificationAsync(
        TicketSmsNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed class TicketSmsNotificationService : ITicketSmsNotificationService
{
    private readonly IFornaxSmsService _fornaxSmsService;
    private readonly ITicketSmsTemplateService _templateService;
    private readonly ILogger<TicketSmsNotificationService> _logger;

    public TicketSmsNotificationService(
        IFornaxSmsService fornaxSmsService,
        ITicketSmsTemplateService templateService,
        ILogger<TicketSmsNotificationService> logger)
    {
        _fornaxSmsService = fornaxSmsService;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task SendTicketStatusNotificationAsync(
        TicketSmsNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.CustomerPhoneNumber))
        {
            _logger.LogWarning(
                "Ticket SMS skipped for ticket {TicketNumber} because customer phone number is missing.",
                notification.TicketNumber);

            return;
        }

        var message = _templateService.BuildStatusMessage(notification);

        await _fornaxSmsService.SendSmsAsync(
            notification.CustomerPhoneNumber,
            message,
            cancellationToken);
    }
}