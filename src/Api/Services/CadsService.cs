// <copyright file="CadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Globalization;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Services.Upstream;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads animals on a holding from the CADS bovine API through a REST strategy.
/// </summary>
public sealed partial class CadsService(
    IRestStrategyFactory<CadsService> strategyFactory,
    IOptions<CadsApiOptions> options,
    ILogger<CadsService> logger)
    : ICadsService
{
    private const string ApiDescription = "CADS bovine animals API";
    private const string AnimalsResource = "api/v1/bovine/animals";
    private const int MaxPages = 100;

    public async Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cph);

        var results = new List<CattleResponse>();
        var page = 1;
        CadsPaginatedResult<CadsAnimal> current;

        do
        {
            current = await GetAnimalsPageAsync(cph, page, cancellationToken);
            results.AddRange((current.Results ?? []).Where(animal => animal.IsAlive).Select(ToCattleResponse));
            page++;
        }
        while (current.HasNextPage && page <= current.TotalPages && page <= MaxPages);

        LogRetrievedLiveAnimalsForHolding(results.Count, page - 1, cph);

        return results;
    }

    private static CattleResponse ToCattleResponse(CadsAnimal animal) => new()
    {
        EarTag = animal.Identifier?.Identifier ?? string.Empty,
        DateBirth = animal.BirthDate,
        DateOnCph = animal.DateOnCph,
        Sex = animal.Sex,
        Breed = animal.BreedCode?.BreedName,
        BreedCode = animal.BreedCode?.Identifier,
        BreedName = animal.BreedCode?.BreedName,
        Status = animal.Status ?? string.Empty,
    };

    private async Task<CadsPaginatedResult<CadsAnimal>> GetAnimalsPageAsync(string cph, int page, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            return await strategyFactory
                .BuildRestStrategy()
                .WithLogger(logger)
                .WithCancellationToken(cancellationToken)
                .WithApiDescription(ApiDescription)
                .WithActionDescription("Get animals on holding")
                .WithBaseUrl(settings.BaseUrl)
                .WithResourceUrl(AnimalsResource)
                .WithBasicAuth(settings.ClientId, settings.ClientSecret)
                .WithJsonSerializerOptions(UpstreamJson.Options)
                .WithGet()
                .WithQueryParameter("CPH", cph)
                .WithQueryParameter("page", page.ToString(CultureInfo.InvariantCulture))
                .WithQueryParameter("pageSize", settings.PageSize.ToString(CultureInfo.InvariantCulture))
                .Execute<CadsPaginatedResult<CadsAnimal>>();
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogHoldingNotKnownToCads(cph);
            throw new NotFoundException($"Holding '{cph}' was not found.");
        }
    }
}
