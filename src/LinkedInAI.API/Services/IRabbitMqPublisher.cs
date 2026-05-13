namespace LinkedInAI.API.Services;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string routingKey, string message);
}
