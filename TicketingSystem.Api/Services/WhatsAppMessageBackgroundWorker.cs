namespace TicketingSystem.Api.Services;

public sealed class WhatsAppMessageBackgroundWorker : BackgroundService
{
    private readonly IWhatsAppMessageQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppMessageBackgroundWorker> _logger;

    public WhatsAppMessageBackgroundWorker(
        IWhatsAppMessageQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppMessageBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsApp message background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            WhatsAppIncomingMessage message;

            try
            {
                message = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var conversationService = scope.ServiceProvider
                    .GetRequiredService<IWhatsAppConversationService>();

                await conversationService.HandleIncomingMessageAsync(
                    message,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process WhatsApp message {MessageId} from {FromPhoneNumber}.",
                    message.MessageId,
                    message.FromPhoneNumber);
            }
        }

        _logger.LogInformation("WhatsApp message background worker stopped.");
    }
}