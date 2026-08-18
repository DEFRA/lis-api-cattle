using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;

namespace Lis.Cattle.Services;

public class CadsService : ICadsService
{
    private readonly HttpClient _httpClient;

    public CadsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph)
    {
        // Endpoint TBC. For now, returning empty or throwing if not configured.
        // In a real scenario, this would call the CADS API.
        var response = await _httpClient.GetAsync($"cattle?cph={cph}");

        if (!response.IsSuccessStatusCode)
        {
            return Enumerable.Empty<CattleResponse>();
        }

        return await response.Content.ReadFromJsonAsync<IEnumerable<CattleResponse>>() ?? Enumerable.Empty<CattleResponse>();
    }
}