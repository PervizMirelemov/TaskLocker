using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TaskLocker.WPF.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;

        public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetStatusAsync()
        {
            var response = await _httpClient.GetAsync("status");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task SendLogAsync(string message)
        {
            var payload = new { Message = message, Timestamp = DateTime.UtcNow };
            var response = await _httpClient.PostAsJsonAsync("logs", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send log. Status: {Status}", response.StatusCode);
            }
        }
    }
}