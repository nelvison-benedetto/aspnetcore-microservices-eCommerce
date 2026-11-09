using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BusinessLogicLayer.RabbitMQ
{
    //Registered as singleton: the connection is opened lazily on the first publish and reused,
    //so the service can start (and serve GET requests) even when RabbitMQ is not reachable yet
    public class RabbitMQPublisher : IRabbitMQPublisher, IAsyncDisposable  //IAsyncDisposable permette di rilasciare in modo asincrono le risorse utilizzate, come connessione e canale RabbitMQ
    {
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _lock = new(1, 1); //il primo 1 indica il numero iniziale di permessi disponibili, il secondo è il numero massimo di permessi. In pratica, una sola chiamata a PublishAsync può utilizzare il canale condiviso alla volta, le altre aspettano.
        private IConnection? _connection; //rappresenta la connessione AMQP tra la tua applicazione .NET e il broker RabbitMQ
        private IChannel? _channel; //rappresenta un canale di comunicazione all'interno della connessione AMQP, utilizzato per inviare e ricevere messaggi, non devi aprire una nuova connessione TCP per ogni messaggio, riutilizzi la stessa connessione e canale.
        private bool _exchangeDeclared; //tiene traccia del fatto che l'exchange sia già stato dichiarato attraverso il canale corrente, inizialmente è false dopo la dichiarazione è true, quando viene creato un nuovo canale viene reimpostato a false (xk la dichiarazione deve essere eseguita nuovamente sul nuovo canale).

        public RabbitMQPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        public async Task PublishAsync<T>(Dictionary<string, object?> headers, T message)
        {
            string messageJson = JsonSerializer.Serialize(message);  //serialize in byte
            byte[] messageBodyInBytes = Encoding.UTF8.GetBytes(messageJson);  //conversione in array di byte (come vuole rabbitmq)

            string exchangeName = _configuration["RabbitMQ_Products_Exchange"]!; //get from the configuration of the app

            //IChannel is not thread-safe for publishing, so access is serialized
            await _lock.WaitAsync();  //ricorda che il channel è condiviso, quindi se arrivano piu richieste contemporaneamente si risolve cosi, WaitAsync() fa aspettare una chiamata finché non ottiene il permesso., Release() lo restituisce.
            try
            {
                IChannel channel = await GetChannelAsync();

                //Create exchange
                if (!_exchangeDeclared)
                {
                    await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Headers, durable: true);  //🔥dichiarazione di un exchange, stai utilizzando un exchande del type di 'headers', durable: true means that the exchange will survive broker restarts, so it will be recreated when RabbitMQ restarts (cmnq se vuoi davvero una persistenza 100% sicura dovresti settare piu cose).
                    _exchangeDeclared = true;
                }

                //Publish message
                var basicProperties = new BasicProperties { Headers = headers };  //BasicProperties è un oggetto che contiene metadati associati alla pubblicazione AMQP, here setti solo gli headers, ma puoi settare anche altre cose come ContentType, DeliveryMode, ecc.

                await channel.BasicPublishAsync(exchange: exchangeName, routingKey: string.Empty, mandatory: false, basicProperties: basicProperties, body: messageBodyInBytes);  //here invii im mex all'exchange , routingKey è vuoto perché stai usando un exchange di tipo headers, mandatory: false means that if the message cannot be routed to any queue xk non ci sono code che corrispondono agli headers allora it will be dropped (non verra restituito al publisher e non riceverai nessun errore).
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<IChannel> GetChannelAsync()  //connessione al canale
        {
            if (_channel is { IsOpen: true })  //se il canale è gia aperto, allora usa quello
                return _channel;  
            if (_connection is not { IsOpen: true })
            {
                ConnectionFactory connectionFactory = new ConnectionFactory()  //ConnectionFactory è l'oggetto del client RabbitMQ che raccoglie i parametri necessari per creare la connessione
                {
                    HostName = _configuration["RabbitMQ_HostName"]!,  //lo prende dai param di configurazione dell'app 
                    UserName = _configuration["RabbitMQ_UserName"]!,
                    Password = _configuration["RabbitMQ_Password"]!,
                    Port = Convert.ToInt32(_configuration["RabbitMQ_Port"])  //di default dovrebbe essere normalmente 5672 (senza TLS)
                };
                _connection = await connectionFactory.CreateConnectionAsync();  //apertura connessione 
            }
            _channel = await _connection.CreateChannelAsync();  //apertuca canale 
            _exchangeDeclared = false;
            return _channel;
        }

        public async ValueTask DisposeAsync()  //chiamato quando la DI elimina questo  Singleton, succede e.g. quando l'applicazione viene turn off.
        {
            if (_channel != null)
                await _channel.DisposeAsync();  //libera
            if (_connection != null)
                await _connection.DisposeAsync();  //libera
            _lock.Dispose(); 
        }
    }
}
