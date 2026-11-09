namespace BusinessLogicLayer.RabbitMQ
{
    public interface IRabbitMQProductDeletionConsumer : IAsyncDisposable
    {
        Task ConsumeAsync(CancellationToken cancellationToken = default);
    }
}
