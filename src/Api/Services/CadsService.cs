using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.Extensions.Logging;

namespace Lis.Cattle.Services;

public class CadsService : ICadsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CadsService>? _logger;

    public CadsService(HttpClient httpClient, ILogger<CadsService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph)
    {
        try
        {
            var response = await _httpClient.GetAsync($"cattle?cph={Uri.EscapeDataString(cph)}");

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("CADS API returned non-success status code {StatusCode} for CPH {Cph}", response.StatusCode, cph);
                return Enumerable.Empty<CattleResponse>();
            }

            return await response.Content.ReadFromJsonAsync<IEnumerable<CattleResponse>>() ?? Enumerable.Empty<CattleResponse>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch cattle from CADS API for CPH {Cph}", cph);
            return Enumerable.Empty<CattleResponse>();
        }
    }
}