using Microsoft.Extensions.Caching.Distributed; //permette di accedere alla cache distribuita e rimuovere le voci.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;  //fornisce le classi per connettersi a RabbitMQ e gestire exchange, code e canali.
using RabbitMQ.Client.Events;  //contiene AsyncEventingBasicConsumer, il consumer che gestisce la ricezione asincrona dei messaggi.
using System.Text;
using System.Text.Json;

namespace BusinessLogicLayer.RabbitMQ
{
    //Registered as singleton: the connection is opened in ConsumeAsync (not in the constructor),
    //so the hosted service can retry while RabbitMQ is still starting

    //consumer RabbitMQ in questo mss, che ascolta gli eventi di eliminazione dei prodotti pubblicati dal microservizio Products. Orders mss non deve cosi ricevere una http req da Products mss ogni volta che viene eliminato un prodotto, invece i 2 mss comunicano tramite broker rabbitmq.
    public class RabbitMQProductDeletionConsumer : IRabbitMQProductDeletionConsumer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQProductDeletionConsumer> _logger;
        private readonly IDistributedCache _cache;
        private IConnection? _connection;  //connessione tra la tua applicazione e il broker RabbitMQ
        private IChannel? _channel;  //🔥canale logico aperto sulla connessione, attraverso cui vengono eseguite operazioni RabbitMQ

        public RabbitMQProductDeletionConsumer(IConfiguration configuration, ILogger<RabbitMQProductDeletionConsumer> logger, IDistributedCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task ConsumeAsync(CancellationToken cancellationToken = default)  //CancellationToken permette al chiamante di chiedere l'interruzione delle operazioni
        {
            //Release a connection left over by a previous failed attempt
            await DisposeAsync();  //prima di creare una nuova connection, rilascia quelle che magari sono rimaste aperte

            ConnectionFactory connectionFactory = new ConnectionFactory()  //ConnectionFactory è l'obj usato dal client RabbitMQ per creare una connessione
            {
                HostName = _configuration["RabbitMQ_HostName"]!,
                UserName = _configuration["RabbitMQ_UserName"]!,
                Password = _configuration["RabbitMQ_Password"]!,
                Port = Convert.ToInt32(_configuration["RabbitMQ_Port"])
            };
            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            //string routingKey = "product.#";
            var headers = new Dictionary<string, object?>()
            {
                { "x-match", "all" },  //tutti gli header indicati devono corrispondere. lo fai xk il tuo systrem di events usando rabbitmq si basa sugli headers, non sulla e.g. routing key.
                { "event", "product.delete" },  //il mex deve evere nell'header l'event 'product.delete', per poter essere accodato here
                { "RowCount", 1 }  //il mex nell'header deve avere RouwCount un valore 1
            }; //contiene i criteri che RabbitMQ deve verificare quando decide se instradare un messaggio verso QUESTA CODA. e.g. Products mss invia messaaggio di evento di cancellazzione di XYZ item, allora qua prendo quel mex e verifico se devo instardarlo in QUESTA coda (che si occupa di fare operazioni di delete anche in questo mss), cosi la cancellazione è coerente sia in Product mss che in Order mss.

            string queueName = "orders.product.delete.queue";

            //Create exchange
            string exchangeName = _configuration["RabbitMQ_Products_Exchange"]!;  //il nome dell'exchange viene letto dalla configurazione dell'app
            await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Headers, durable: true, cancellationToken: cancellationToken);  //dichiarazione di un Exchange. durable(la sua definizione sopravvive a un riavvio del broker) 

            //Create message queue
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken); //x-message-ttl | x-max-length | x-expired. 
            //la queue in cui rabbitmq conserverà i mex destinati al consumer.
            //exclusive: false (la coda non è riservata esclusivamente alla connessione che l'ha dichiarata), autoDelete: false (la coda non viene eliminata automaticamente quando l'ultimo consumer si scollega), arguments: null (non vengono impostati argomenti aggiuntivi e.g. dead-letter exchange or TTL.

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: string.Empty, arguments: headers, cancellationToken: cancellationToken);
            //collegamento exchange-queue, in questo modo i mexs dell'exchange che soddifano tutti i criteri ok, vengono passati a questa queue orders.product.delete.queue. routingKey:string.Empty (tu stai usando gli exchange headers, quindi la routing key non è il criterio principale di instradamento)

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);  //obj che riceverà i mexs della queue

            consumer.ReceivedAsync += async (sender, args) => //funct da eseguire OGNI VOLTA che un consumer riceve un mex. sender è obj che ha generato l'evento, args contiene i datas del mex (body, ect)
            {
                try
                {
                    string message = Encoding.UTF8.GetString(args.Body.ToArray());  //bytes->string
                    ProductDeletionMessage? productDeletionMessage = JsonSerializer.Deserialize<ProductDeletionMessage>(message);  //string json -> C# obj
                    if (productDeletionMessage != null)
                    {
                        _logger.LogInformation($"Product deleted: {productDeletionMessage.ProductID}, Product name: {productDeletionMessage.ProductName}");
                        await HandleProductDeletion(productDeletionMessage.ProductID); //urs c rimuivere la voce dalla cache
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing a product.delete message");
                }
            };
            await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken);
        }

        private async Task HandleProductDeletion(Guid productID)  //logica vera e propria del consumer, cioe cancellare dalla cache il target prodotto, in questo modo Orders non utilizzera piu datas cachati di un prodotto eliminato.
        {
            string cacheKeyToWrite = $"product:{productID}";
            await _cache.RemoveAsync(cacheKeyToWrite);
        }

        public async ValueTask DisposeAsync()  //rilascia le risorse
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
