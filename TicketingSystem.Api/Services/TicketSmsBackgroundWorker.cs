//This worker runs in the background as long as your API is running. 
//It waits for queued SMS notifications and sends them outside the user’s request.

using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public sealed class TicketSmsBackgroundWorker : BackgroundService
{
    private readonly ITicketSmsQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketSmsBackgroundWorker> _logger;

    public TicketSmsBackgroundWorker(
        ITicketSmsQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TicketSmsBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var notification = await _queue.DequeueAsync(stoppingToken);

            // DUPLICATE CHECK - Before any retry attempts
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var alreadySent = await context.NotificationLogs
                    .AnyAsync(log => log.EventKey == notification.EventKey, stoppingToken);

                if (alreadySent)
                {
                    _logger.LogInformation(
                        "Skipping duplicate SMS notification {EventKey} for ticket {TicketNumber}.",
                        notification.EventKey,
                        notification.TicketNumber);
                    continue; // Skip this notification entirely
                }
            }

            // RETRY LOGIC with duplicate logging on success
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var sender = scope.ServiceProvider
                        .GetRequiredService<ITicketSmsNotificationService>();

                    await sender.SendTicketStatusNotificationAsync(
                        notification,
                        stoppingToken);

                    // SUCCESS - Log the notification to prevent future duplicates
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    context.NotificationLogs.Add(new NotificationLog
                    {
                        Id = Guid.NewGuid(),
                        EventKey = notification.EventKey,
                        Channel = "SMS",
                        Recipient = notification.CustomerPhoneNumber,
                        CreatedAt = DateTime.UtcNow
                    });
                    
                    await context.SaveChangesAsync(stoppingToken);
                    
                    _logger.LogInformation(
                        "SMS sent and logged for ticket {TicketNumber}, EventKey: {EventKey}",
                        notification.TicketNumber,
                        notification.EventKey);

                    break; // Success - exit retry loop
                }
                catch (Exception ex) when (attempt < 3)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to send SMS for ticket {TicketNumber}. Retry attempt {Attempt}.",
                        notification.TicketNumber,
                        attempt);

                    await Task.Delay(TimeSpan.FromSeconds(attempt * 3), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "SMS permanently failed for ticket {TicketNumber} after 3 attempts.",
                        notification.TicketNumber);
                }
            }
        }
    }
}



// using Microsoft.EntityFrameworkCore;
// using TicketingSystem.Api.Data;
// using TicketingSystem.Api.Models;


// namespace TicketingSystem.Api.Services;

// public sealed class TicketSmsBackgroundWorker : BackgroundService
// {
//     private readonly ITicketSmsQueue _queue;
//     private readonly IServiceScopeFactory _scopeFactory;
//     private readonly ILogger<TicketSmsBackgroundWorker> _logger;

//     public TicketSmsBackgroundWorker(
//         ITicketSmsQueue queue,
//         IServiceScopeFactory scopeFactory,
//         ILogger<TicketSmsBackgroundWorker> logger)
//     {
//         _queue = queue;
//         _scopeFactory = scopeFactory;
//         _logger = logger;
//     }

//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         while (!stoppingToken.IsCancellationRequested)
//         {
//             var notification = await _queue.DequeueAsync(stoppingToken);

//             for (var attempt = 1; attempt <= 3; attempt++)
//             {
//                 try
//                 {
//                     using var scope = _scopeFactory.CreateScope();

//                     var sender = scope.ServiceProvider
//                         .GetRequiredService<ITicketSmsNotificationService>();

//                     await sender.SendTicketStatusNotificationAsync(
//                         notification,
//                         stoppingToken);

//                     break;
//                 }
//                 catch (Exception ex) when (attempt < 3)
//                 {
//                     _logger.LogWarning(
//                         ex,
//                         "Failed to send SMS for ticket {TicketNumber}. Retry attempt {Attempt}.",
//                         notification.TicketNumber,
//                         attempt);

//                     await Task.Delay(TimeSpan.FromSeconds(attempt * 3), stoppingToken);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(
//                         ex,
//                         "SMS permanently failed for ticket {TicketNumber}.",
//                         notification.TicketNumber);
//                 }
//             }
//         }
//     }
// }

// //This worker runs in the background as long as your API is running. 
// //It waits for queued SMS notifications and sends them outside the user’s request.