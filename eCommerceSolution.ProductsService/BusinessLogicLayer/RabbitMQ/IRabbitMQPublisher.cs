namespace BusinessLogicLayer.RabbitMQ
{
    public interface IRabbitMQPublisher
    {
        Task PublishAsync<T>(Dictionary<string, object?> headers, T message);
    }
}
