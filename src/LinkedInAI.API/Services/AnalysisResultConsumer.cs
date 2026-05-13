using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LinkedInAI.API.Services;

/// <summary>
/// Background hosted service that consumes analysis results from RabbitMQ
/// and persists them to the database.
/// </summary>
public class AnalysisResultConsumer(
    IServiceProvider services,
    IConnection connection,
    ILogger<AnalysisResultConsumer> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync("linkedin.analysis", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync("linkedin.analysis.results", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync("linkedin.analysis.results", "linkedin.analysis", "analysis.result.#", cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            logger.LogInformation("Received analysis result: {RoutingKey}", ea.RoutingKey);

            try
            {
                var snakeOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                };
                var msg = JsonSerializer.Deserialize<AnalysisResultMessage>(body, snakeOptions);

                if (msg != null)
                {
                    await using var scope = services.CreateAsyncScope();
                    var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();

                    string? resultJson = null;
                    int? score = msg.OverallScore;

                    if (msg.Result.HasValue)
                    {
                        resultJson = JsonSerializer.Serialize(msg.Result.Value);
                        // Fallback: extract score from nested result if not provided at top level
                        if (score == null &&
                            msg.Result.Value.TryGetProperty("profile_score", out var ps) &&
                            ps.TryGetProperty("overall_score", out var s))
                            score = s.GetInt32();
                    }

                    await analysisService.UpdateResultAsync(
                        msg.MessageId, msg.Status, resultJson, score, msg.ErrorMessage);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process analysis result");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync("linkedin.analysis.results", autoAck: false, consumer, cancellationToken: stoppingToken);

        logger.LogInformation("Analysis result consumer started");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            await _channel.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}

public record AnalysisResultMessage(
    string MessageId,
    int UserId,
    int ProfileId,
    string Status,
    JsonElement? Result,
    int? OverallScore,
    string? ErrorMessage
);
