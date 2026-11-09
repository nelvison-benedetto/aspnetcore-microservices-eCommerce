using Microsoft.Extensions.Hosting;  //contiene BackgroundService, x eseguire attività in background gestite dall'host ASP.NET Core
using Microsoft.Extensions.Logging;

namespace BusinessLogicLayer.RabbitMQ
{
    //gestisce avvio/stop/relase di consumer RabbitMQProductDeletionConsumer.
    public class RabbitMQProductDeletionHostedService : BackgroundService
    {
        private readonly IRabbitMQProductDeletionConsumer _productDeletionConsumer;
        private readonly ILogger<RabbitMQProductDeletionHostedService> _logger;

        public RabbitMQProductDeletionHostedService(IRabbitMQProductDeletionConsumer consumer, ILogger<RabbitMQProductDeletionHostedService> logger)
        {
            _productDeletionConsumer = consumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Retry until RabbitMQ is reachable (e.g. the container is still starting)
            while (!stoppingToken.IsCancellationRequested)  //continua a eseguire il ciclo finché non è stata richiesta la cancellazione del servizio
            {
                try
                {
                    await _productDeletionConsumer.ConsumeAsync(stoppingToken);  //esgue il consumer dell'altro file
                    _logger.LogInformation("Listening for product.delete messages");
                    return;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("RabbitMQ not reachable ({Message}). Retrying in 5 seconds...", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);  //dopo 5sec il ciclo riparte
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await _productDeletionConsumer.DisposeAsync();
        }
    }
}
