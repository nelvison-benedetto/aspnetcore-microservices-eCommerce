namespace BusinessLogicLayer.RabbitMQ
{
    public interface IRabbitMQProductNameUpdateConsumer : IAsyncDisposable
    {
        Task ConsumeAsync(CancellationToken cancellationToken = default);
    }
}
