using DataLayer.Models;
using DataLayer.Models.DTOs;
using SearchDaemon.RabbitMQ;
using System.Collections.Concurrent;

namespace ServicesLayer
{
    public class WebMixerService : IWebMixerService
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<WebMixerService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _sentMessages = new();

        public WebMixerService(IRabbitMQService rabbitMQService, ILogger<WebMixerService> logger)
        {
            _rabbitMQService = rabbitMQService ?? throw new ArgumentNullException(nameof(rabbitMQService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Search(SearchRequestDto request)
        {
            try
            {
                // Crear un identificador único basado en los parámetros de búsqueda
                var messageId = CreateMessageId(request);

                // Verificar si el mensaje ya fue enviado recientemente
                if (_sentMessages.TryGetValue(messageId, out var lastSent))
                {
                    if (DateTime.UtcNow.Subtract(lastSent).TotalMinutes < request.Frequency)
                    {
                        _logger.LogWarning("Búsqueda ignorada por ser demasiado reciente: {MessageId}", messageId);
                        return;
                    }
                }

                _logger.LogInformation("Publishing search request with keywords: {Keywords}", request.Keywords);

                // Publicar el mensaje en la cola
                
                    
                 await _rabbitMQService.PublishMessageAsync(
                    "scrapper_request_queue",
                    request
                );

                // Registrar el mensaje enviado
                _sentMessages.AddOrUpdate(messageId, DateTime.UtcNow, (key, existingValue) => DateTime.UtcNow);

                // Programar limpieza después de un tiempo
                _ = Task.Delay(TimeSpan.FromHours(1))
                    .ContinueWith(_ =>
                    {
                        DateTime removedTime;
                        _sentMessages.TryRemove(messageId, out removedTime);
                    });

                _logger.LogInformation("Search request published successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during search operation");
                throw;
            }
        }

        private string CreateMessageId(SearchRequestDto request)
        {
            var components = new[]
            {
                request.SearchId.ToString(),
                request.Keywords,
                request.Category?.ToString() ?? "0",
                request.MinPrice?.ToString() ?? "0",
                request.MaxPrice?.ToString() ?? "0",
                string.Join(",", request.PlatformIds ?? new List<int>())
            };

            return string.Join("_", components);
        }
    }
}