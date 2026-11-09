using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BusinessLogicLayer.RabbitMQ
{
    //Registered as singleton: the connection is opened in ConsumeAsync (not in the constructor),
    //so the hosted service can retry while RabbitMQ is still starting
    public class RabbitMQProductNameUpdateConsumer : IRabbitMQProductNameUpdateConsumer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQProductNameUpdateConsumer> _logger;
        private readonly IDistributedCache _cache;
        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMQProductNameUpdateConsumer(IConfiguration configuration, ILogger<RabbitMQProductNameUpdateConsumer> logger, IDistributedCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task ConsumeAsync(CancellationToken cancellationToken = default)
        {
            await DisposeAsync();
            ConnectionFactory connectionFactory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ_HostName"]!,
                UserName = _configuration["RabbitMQ_UserName"]!,
                Password = _configuration["RabbitMQ_Password"]!,
                Port = Convert.ToInt32(_configuration["RabbitMQ_Port"])
            };
            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            //string routingKey = "product.update.*";
            var headers = new Dictionary<string, object?>()
            {
                { "x-match", "all" },
                { "event", "product.update" },
                { "RowCount", 1 }
            };
            string queueName = "orders.product.update.name.queue";
            //Create exchange
            string exchangeName = _configuration["RabbitMQ_Products_Exchange"]!;
            await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Headers, durable: true, cancellationToken: cancellationToken);
            //Create message queue
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken); //x-message-ttl | x-max-length | x-expired
            //Bind the message to exchange
            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: string.Empty, arguments: headers, cancellationToken: cancellationToken);
            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, args) =>
            {
                try
                {
                    string message = Encoding.UTF8.GetString(args.Body.ToArray());
                    ProductDTO? productDTO = JsonSerializer.Deserialize<ProductDTO>(message);
                    if (productDTO != null)
                    {
                        await HandleProductUpdation(productDTO);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing a product.update message");
                }
            };
            await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken);
        }

        private async Task HandleProductUpdation(ProductDTO productDTO)
        {
            _logger.LogInformation($"Product name updated: {productDTO.ProductID}, New name: {productDTO.ProductName}");
            string productJson = JsonSerializer.Serialize(productDTO);
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
              .SetAbsoluteExpiration(TimeSpan.FromSeconds(300));
            //🔥qua avviene il vero e proprio cosa deve fare il consumer, cioe update il valore cache del tyarget product
            string cacheKeyToWrite = $"product:{productDTO.ProductID}";
            await _cache.SetStringAsync(cacheKeyToWrite, productJson, options);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
                await _channel.DisposeAsync();
            if (_connection != null)
                await _connection.DisposeAsync();
            _channel = null;
            _connection = null;
        }
    }
}
