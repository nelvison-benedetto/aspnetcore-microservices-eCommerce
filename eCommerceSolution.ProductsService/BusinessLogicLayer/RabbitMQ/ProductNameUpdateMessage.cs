namespace BusinessLogicLayer.RabbitMQ
{
    public record ProductNameUpdateMessage(Guid ProductID, string? NewName);
}
