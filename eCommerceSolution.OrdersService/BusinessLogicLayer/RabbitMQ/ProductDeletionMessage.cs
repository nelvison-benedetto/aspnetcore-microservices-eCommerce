namespace BusinessLogicLayer.RabbitMQ
{
    public record ProductDeletionMessage(Guid ProductID, string? ProductName);
}
