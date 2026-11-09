using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessLogicLayer.RabbitMQ
{
    // gestisce avvio/stop/relase di consumer RabbitMQProductNameUpdateConsumer
    public class RabbitMQProductNameUpdateHostedService : BackgroundService
    {
        private readonly IRabbitMQProductNameUpdateConsumer _productNameUpdateConsumer;
        private readonly ILogger<RabbitMQProductNameUpdateHostedService> _logger;

        public RabbitMQProductNameUpdateHostedService(IRabbitMQProductNameUpdateConsumer consumer, ILogger<RabbitMQProductNameUpdateHostedService> logger)
        {
            _productNameUpdateConsumer = consumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Retry until RabbitMQ is reachable (e.g. the container is still starting)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _productNameUpdateConsumer.ConsumeAsync(stoppingToken);
                    _logger.LogInformation("Listening for product.update messages");
                    return;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("RabbitMQ not reachable ({Message}). Retrying in 5 seconds...", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await _productNameUpdateConsumer.DisposeAsync();
        }
    }
}
