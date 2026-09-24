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
/// Reads animals on a holding, and the details of a single animal, from the CADS bovine API
/// through a REST strategy.
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
    private const string GeneticDamType = "genetic";
    private const string SurrogateDamType = "surrogate";

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

    public async Task<CattleDetailsResponse> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        var settings = options.Value;
        CadsAnimalDetailResponse response;

        try
        {
            response = await strategyFactory
                .BuildRestStrategy()
                .WithLogger(logger)
                .WithCancellationToken(cancellationToken)
                .WithApiDescription(ApiDescription)
                .WithActionDescription("Get animal details")
                .WithBaseUrl(settings.BaseUrl)
                .WithResourceUrl($"{AnimalsResource}/{Uri.EscapeDataString(earTag)}")
                .WithBasicAuth(settings.ClientId, settings.ClientSecret)
                .WithJsonSerializerOptions(UpstreamJson.Options)
                .WithGet()
                .Execute<CadsAnimalDetailResponse>();
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogAnimalNotKnownToCads(earTag);
            throw new NotFoundException($"Animal '{earTag}' was not found.");
        }

        if (response.AnimalDetail is null)
        {
            LogAnimalNotKnownToCads(earTag);
            throw new NotFoundException($"Animal '{earTag}' was not found.");
        }

        LogRetrievedAnimalDetails(earTag);

        return ToCattleDetailsResponse(earTag, response.AnimalDetail);
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

    private static CattleDetailsResponse ToCattleDetailsResponse(string earTag, CadsAnimalDetail detail)
    {
        var parentage = detail.Parentage ?? [];
        var geneticDam = EarTagOf(parentage, CadsParentage.GeneticDam);
        var surrogateDam = EarTagOf(parentage, CadsParentage.SurrogateDam);

        return new CattleDetailsResponse
        {
            EarTag = detail.Identifier?.Identifier ?? earTag,
            Species = detail.Species,
            Sex = detail.Sex,
            DateBirth = detail.BirthDate,
            DateRegistered = detail.RegistrationDate,
            DateOnCph = detail.DateOnCph,
            Breed = detail.BreedCode?.BreedName,
            BreedCode = detail.BreedCode?.Identifier,
            BreedName = detail.BreedCode?.BreedName,
            State = detail.State,
            RestrictionStatus = detail.RestrictionStatus,
            DamType = DamTypeOf(geneticDam, surrogateDam),
            GeneticDamEarTag = geneticDam,
            SurrogateDamEarTag = surrogateDam,
            SireEarTag = EarTagOf(parentage, CadsParentage.Sire),

            // CADS does not carry a sire name; the field exists for the consuming services' contract.
            SireName = null,
        };
    }

    private static string? EarTagOf(IReadOnlyList<CadsParentage> parentage, string relationship) =>
        parentage.FirstOrDefault(parent => parent.Is(relationship))?.AnimalIdentifier?.Identifier;

    private static string? DamTypeOf(string? geneticDam, string? surrogateDam)
    {
        if (!string.IsNullOrWhiteSpace(surrogateDam))
        {
            return SurrogateDamType;
        }

        return string.IsNullOrWhiteSpace(geneticDam) ? null : GeneticDamType;
    }

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
