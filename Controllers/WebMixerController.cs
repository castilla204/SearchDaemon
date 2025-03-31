using Microsoft.AspNetCore.Mvc;
using ServicesLayer;
using DataLayer.Models;
using DataLayer.Models.DTOs;

namespace SearchDaemon.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebMixerController : ControllerBase
    {
        private readonly IWebMixerService _webMixerService;
        private readonly ILogger<WebMixerController> _logger;

        public WebMixerController(IWebMixerService webMixerService, ILogger<WebMixerController> logger)
        {
            _webMixerService = webMixerService ?? throw new ArgumentNullException(nameof(webMixerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("Search")]
        public async Task<IActionResult> Search(
            [FromQuery] string keywords,
            [FromQuery] string userSearch,
            [FromQuery] int pagestoscrape,
            [FromQuery] List<int> platformIds,
            [FromQuery] int? category = null,
            [FromQuery] string? latitude = null,
            [FromQuery] string? longitude = null,
            [FromQuery] int? locationrange = null,
            [FromQuery] int? minprice = null,
            [FromQuery] int? maxprice = null,
            [FromQuery] int? brandId = null,
            [FromQuery] int? modelId = null,
            [FromQuery] bool analyze = false,
            [FromQuery] bool isMultiPage = false,
            [FromQuery] bool shippingAviable = false

       )
        {
            try
            {
                _logger.LogInformation("Received search request with keywords: {Keywords}", keywords);

                var searchRequest = new SearchRequestDto
                {
                    Keywords = keywords,
                    UserSearch = userSearch,
                    PagesToScrape = pagestoscrape,
                    PlatformIds = platformIds,
                    Category = category,
                    Latitude = latitude,
                    Longitude = longitude,
                    MinPrice = minprice,
                    MaxPrice = maxprice,
                    BrandId = brandId,
                    ModelId = modelId,
                    Analyze = analyze,
                    IsMultiPage = isMultiPage,
                    ShippingAvailable = shippingAviable,
                    IsProgrammed = false
                };

                _webMixerService.Search(searchRequest);

                return Ok();

                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing search request");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}