using System.Text;
using RabbitMQ.Client;

namespace LinkedInAI.API.Services;

public class RabbitMqPublisher(IConnection connection, ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher, IAsyncDisposable
{
    private IChannel? _channel;

    private async Task<IChannel> GetChannelAsync()
    {
        if (_channel == null || _channel.IsClosed)
        {
            _channel = await connection.CreateChannelAsync();
            await _channel.ExchangeDeclareAsync(
                exchange: "linkedin.analysis",
                type: ExchangeType.Topic,
                durable: true);
        }
        return _channel;
    }

    public async Task PublishAsync(string routingKey, string message)
    {
        var channel = await GetChannelAsync();
        var body = Encoding.UTF8.GetBytes(message);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString()
        };

        await channel.BasicPublishAsync(
            exchange: "linkedin.analysis",
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body);

        logger.LogDebug("Published message to {Exchange}/{RoutingKey}", "linkedin.analysis", routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.DisposeAsync();
    }
}
