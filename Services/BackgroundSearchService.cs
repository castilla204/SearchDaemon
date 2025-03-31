using DataLayer.Models;
using DataLayer.Models.DTOs;
using DataLayer.Models.PostGresModels;
using Microsoft.EntityFrameworkCore;
using ServicesLayer;

namespace SearchDaemon.Services
{
    public class BackgroundSearchService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundSearchService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public BackgroundSearchService(
            IServiceProvider serviceProvider,
            ILogger<BackgroundSearchService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSearches(stoppingToken);
                    _logger.LogInformation("Búsqueda programada verificada");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing searches");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessSearches(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var webMixerService = scope.ServiceProvider.GetRequiredService<IWebMixerService>();

            var now = DateTime.UtcNow;
            var searches = await dbContext.Searches
                .Include(s => s.SearchParameters)
                .ThenInclude(sp => sp.SearchParameterPlatforms)
                .Where(s => s.IsActive && s.StartDate <= now && s.NextExecution <= now)
                .ToListAsync(stoppingToken);

            foreach (var search in searches)
            {
                try
                {
                    foreach (var param in search.SearchParameters)
                    {
                        var platformIds = param.SearchParameterPlatforms
                            .Select(p => p.PlatformId)
                            .ToList();

                        if (platformIds.Any())
                        {
                            var request = new SearchRequestDto
                            {
                                SearchId = search.Id,
                                Keywords = param.Keywords,
                                UserSearch = param.UserSearch,
                                PagesToScrape = 1,
                                PlatformIds = platformIds,
                                Category = param.Category,
                                Latitude = param.Latitude,
                                Longitude = param.Longitude,
                                MinPrice = param.MinPrice,
                                MaxPrice = param.MaxPrice,
                                BrandId = param.BrandId,
                                ModelId = param.ModelId,
                                ShippingAvailable = param.ShippingAvailable,
                                IsProgrammed = true
                            };

                            await webMixerService.Search(request);
                            _logger.LogInformation($"Búsqueda {search.Id} enviada a la cola");
                        }
                    }

                    // Actualizar última ejecución y programar la siguiente
                    search.LastExecution = now;
                    search.NextExecution = now.AddMinutes(search.Frequency);

                    try
                    {
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Búsqueda {search.Id} actualizada con éxito");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error actualizando la búsqueda {search.Id}");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing search {SearchId}", search.Id);
                }
            }
        }
    }
}