using System.Threading.Channels;

namespace TicketingSystem.Api.Services;

public interface ITicketSmsQueue
{
    void Enqueue(TicketSmsNotification notification);
    ValueTask<TicketSmsNotification> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class TicketSmsQueue : ITicketSmsQueue
{
    private readonly Channel<TicketSmsNotification> _channel =
        Channel.CreateUnbounded<TicketSmsNotification>();

    public void Enqueue(TicketSmsNotification notification)
    {
        _channel.Writer.TryWrite(notification);
    }

    public ValueTask<TicketSmsNotification> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}