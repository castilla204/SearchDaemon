using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Connections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using IConnectionFactory = RabbitMQ.Client.IConnectionFactory;

namespace SearchDaemon.RabbitMQ
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly SemaphoreSlim _channelSemaphore;
        private readonly Dictionary<string, QueueConfiguration> _queueConfigs;

        public RabbitMQService(IConnectionFactory connectionFactory, ILogger<RabbitMQService> logger)
        {
            _logger = logger;
            _channelSemaphore = new SemaphoreSlim(1, 1);
            _queueConfigs = new Dictionary<string, QueueConfiguration>();

            try
            {
                var factory = (ConnectionFactory)connectionFactory;
                factory.RequestedHeartbeat = TimeSpan.FromSeconds(60);
                factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
                factory.AutomaticRecoveryEnabled = true;
                factory.RequestedConnectionTimeout = TimeSpan.FromSeconds(30);

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Configure QoS
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                // Configurar colas principales
                ConfigureQueue("scrapper_request_queue", false, false, "10m");

                _logger.LogInformation("RabbitMQ connection initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RabbitMQ connection");
                throw;
            }
        }

        private void ConfigureQueue(string queueName, bool durable, bool autoDelete, string messageTtl)
        {
            var arguments = new Dictionary<string, object>
            {
                { "x-message-ttl", 300000 }, // 5 minutos
                { "x-max-length", 100000 }, // Aumentado para manejar más mensajes
                { "x-overflow", "reject-publish" }
            };

            _queueConfigs[queueName] = new QueueConfiguration
            {
                Durable = durable,
                AutoDelete = autoDelete,
                Arguments = arguments
            };

            _channel.QueueDeclare(
                queue: queueName,
                durable: durable,
                exclusive: false,
                autoDelete: autoDelete,
                arguments: arguments);
        }

        private int ParseTtl(string ttl)
        {
            var value = int.Parse(ttl.Substring(0, ttl.Length - 1));
            var unit = ttl[ttl.Length - 1];
            return unit switch
            {
                's' => value * 1000,
                'm' => value * 60000,
                'h' => value * 3600000,
                'd' => value * 86400000,
                _ => throw new ArgumentException($"Invalid TTL unit: {unit}")
            };
        }

        private void EnsureQueueExists(string queueName)
        {
            try
            {
                // Verify queue exists with correct arguments
                var arguments = new Dictionary<string, object>
                {
                    { "x-message-ttl", 300000 }, // 5 minutos
                    { "x-max-length", 100000 },
                    { "x-overflow", "reject-publish" }
                };

                _channel.QueueDeclare(
                    queue: queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: arguments);

                _logger.LogInformation($"Queue {queueName} declared/verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error declaring queue {queueName}");
                throw;
            }
        }

        public async Task PublishMessageAsync<T>(string queueName, T message)
        {
            try
            {
                await _channelSemaphore.WaitAsync();

                try
                {
                    EnsureQueueExists(queueName);

                    var json = JsonConvert.SerializeObject(message);
                    var body = Encoding.UTF8.GetBytes(json);

                    var props = _channel.CreateBasicProperties();
                    props.Persistent = _queueConfigs[queueName].Durable;
                    props.ContentType = "application/json";
                    props.DeliveryMode = _queueConfigs[queueName].Durable ? (byte)2 : (byte)1;
                    props.MessageId = Guid.NewGuid().ToString();
                    props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    props.Headers = new Dictionary<string, object>
                    {
                        { "x-first-death-queue", queueName },
                        { "x-first-death-reason", "rejected" }
                    };

                    _channel.BasicPublish(
                        exchange: "",
                        routingKey: queueName,
                        basicProperties: props,
                        body: body);

                    _logger.LogInformation($"Message published to queue {queueName}");
                }
                finally
                {
                    _channelSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing message to queue {queueName}");
                throw;
            }
        }

        private class QueueConfiguration
        {
            public bool Durable { get; set; }
            public bool AutoDelete { get; set; }
            public Dictionary<string, object> Arguments { get; set; }
        }

        public void Dispose()
        {
            try
            {
                if (_channel?.IsOpen == true)
                {
                    _channel.Close();
                }
                _channel?.Dispose();

                if (_connection?.IsOpen == true)
                {
                    _connection.Close();
                }
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ resources");
            }
        }
    }
}