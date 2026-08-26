// <copyright file="CadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Microsoft.Extensions.Logging;

public class CadsService(
    HttpClient httpClient,
    ILogger<CadsService>? logger = null)
    : ICadsService
{
    public async Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph)
    {
        try
        {
            var response = await httpClient.GetAsync($"cattle?cph={Uri.EscapeDataString(cph)}");

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("CADS API returned non-success status code {StatusCode} for CPH {Cph}", response.StatusCode, cph);
                return Enumerable.Empty<CattleResponse>();
            }

            return await response.Content.ReadFromJsonAsync<IEnumerable<CattleResponse>>() ?? Enumerable.Empty<CattleResponse>();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fetch cattle from CADS API for CPH {Cph}", cph);
            return Enumerable.Empty<CattleResponse>();
        }
    }
}
