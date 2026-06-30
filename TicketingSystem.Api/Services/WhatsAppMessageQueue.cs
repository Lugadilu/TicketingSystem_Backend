using System.Threading.Channels;

namespace TicketingSystem.Api.Services;

public interface IWhatsAppMessageQueue
{
    void Enqueue(WhatsAppIncomingMessage message);
    ValueTask<WhatsAppIncomingMessage> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class WhatsAppMessageQueue : IWhatsAppMessageQueue
{
    private readonly Channel<WhatsAppIncomingMessage> _channel =
        Channel.CreateUnbounded<WhatsAppIncomingMessage>();

    public void Enqueue(WhatsAppIncomingMessage message)
    {
        _channel.Writer.TryWrite(message);
    }

    public ValueTask<WhatsAppIncomingMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}