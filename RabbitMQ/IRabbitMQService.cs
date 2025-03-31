namespace SearchDaemon.RabbitMQ
{
    public interface IRabbitMQService : IDisposable
    {
        Task PublishMessageAsync<T>(string queueName, T message);
    }
}