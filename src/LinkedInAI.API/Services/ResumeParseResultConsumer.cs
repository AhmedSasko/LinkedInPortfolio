using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LinkedInAI.API.Services;

public class ResumeParseResultConsumer(
    IServiceProvider services,
    IConnection connection,
    ILogger<ResumeParseResultConsumer> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync("linkedin.analysis", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync("linkedin.resume.parse.results", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync("linkedin.resume.parse.results", "linkedin.analysis", "resume.parse.result", cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            logger.LogInformation("Received resume parse result: {RoutingKey}", ea.RoutingKey);

            try
            {
                var snakeOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                };
                var msg = JsonSerializer.Deserialize<ResumeParseResultMessage>(body, snakeOptions);

                if (msg != null)
                {
                    await using var scope = services.CreateAsyncScope();
                    var resumeService = scope.ServiceProvider.GetRequiredService<IResumeService>();

                    string? parsedDataJson = null;
                    if (msg.ParsedData.HasValue)
                        parsedDataJson = JsonSerializer.Serialize(msg.ParsedData.Value);

                    await resumeService.UpdateParseResultAsync(
                        msg.ResumeId, msg.Status, parsedDataJson, msg.ErrorMessage);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process resume parse result");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync("linkedin.resume.parse.results", autoAck: false, consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Resume parse result consumer started");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            await _channel.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}

public record ResumeParseResultMessage(
    string MessageId,
    int UserId,
    int ResumeId,
    string Status,
    JsonElement? ParsedData,
    string? ErrorMessage
);
