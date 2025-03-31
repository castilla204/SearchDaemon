using DataLayer.Models;
using DataLayer.Models.DTOs;

namespace ServicesLayer
{
    public interface IWebMixerService
    {
        public Task Search(SearchRequestDto request);
    }
}