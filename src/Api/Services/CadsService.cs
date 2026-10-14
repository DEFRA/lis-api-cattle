// <copyright file="CadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Globalization;
using Amazon.Runtime;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Models.Responses;
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
public sealed partial class CadsService : ICadsService
{
    private const string AnimalsResource = "api/v1/bovine/animals";
    private const string GeneticDamType = "genetic";
    private const string SurrogateDamType = "surrogate";
    private const int MaxPages = 100;

    private readonly IRestStrategyFactory<CadsService> strategyFactory;
    private readonly IOptions<CadsApiOptions> options;
    private readonly IOptions<CadsDataPatchOptions> patchOptions;
    private readonly ICadsDataPatchStore dataPatchStore;
    private readonly ILogger<CadsService> logger;

    public CadsService(
        IRestStrategyFactory<CadsService> strategyFactory,
        IOptions<CadsApiOptions> options,
        IOptions<CadsDataPatchOptions> patchOptions,
        ICadsDataPatchStore dataPatchStore,
        ILogger<CadsService> logger)
    {
        this.strategyFactory = strategyFactory;
        this.options = options;
        this.patchOptions = patchOptions;
        this.dataPatchStore = dataPatchStore;
        this.logger = logger;

        this.strategyFactory
            .WithDefaultLogger(logger)
            .WithDefaultApiDescription("CADS bovine animals API")
            .WithDefaultBaseUrl(options.Value.BaseUrl)
            .WithDefaultBasicAuth(options.Value.ClientId, options.Value.ClientSecret)
            .WithDefaultJsonSerializerOptions(UpstreamJson.Options);
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(
        string cph,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cph);

        var results = new List<CattleResponse>();
        var page = 1;
        CadsAnimalsOnHolding current;
        IReadOnlyList<CadsAnimal> animals;

        do
        {
            current = await GetAnimalsPagedAsync(cph, page, options.Value.PageSize, cancellationToken);
            animals = current.Animals ?? [];
            results.AddRange(animals.Select(ToCattleResponse));
            page++;
        }
        while (animals.Count > 0 && (long)(page - 1) * current.PageSize < current.TotalRecords && page <= MaxPages);

        LogRetrievedLiveAnimalsForHolding(results.Count, page - 1, cph);

        // Temporary data patch for CADS: only once every page is read, so an animal on a later page is not patched in twice.
        if (patchOptions.Value.IsEnabled)
        {
            await EnrichData(cph, results, cancellationToken);
        }

        return results;
    }

    public async Task<CattleDetailsResponse> GetAnimalDetailsAsync(
        string earTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        CadsAnimalDetailResponse? response;

        try
        {
            response = await strategyFactory
                .BuildRestStrategy()
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Get animal details")
                .WithResourceUrl($"{AnimalsResource}/{Uri.EscapeDataString(earTag)}")
                .WithGet()
                .Execute<CadsAnimalDetailResponse>();
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            // Temporary data patch for CADS, check in the patch cache incase.
            if (patchOptions.Value.IsEnabled)
            {
                var animalDetail = await dataPatchStore.GetAnimalDetailsAsync(earTag, cancellationToken);
                if (animalDetail == null)
                {
                    LogAnimalNotKnownToCads(earTag);
                    throw new NotFoundException($"Animal '{earTag}' was not found.");
                }

                response = new CadsAnimalDetailResponse(null, null, null, null, animalDetail);
            }
            else
            {
                LogAnimalNotKnownToCads(earTag);
                throw new NotFoundException($"Animal '{earTag}' was not found.");
            }
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
            CurrentLocation = ToLocationResponse(detail.CurrentLocation),
            RegisteredLocation = ToLocationResponse(detail.RegisteredLocation),
            DeathLocation = ToLocationResponse(detail.DeathLocation),
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

    private static CattleLocationResponse? ToLocationResponse(CadsLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        return new CattleLocationResponse
        {
            Identifier = location.Identifier,
            SubIdentifier = location.SubIdentifier,
            FullIdentifier = location.FullIdentifier,
            Name = location.Name,
            PremisesType = location.PremisesType,
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

    /// <summary>
    /// Adds the live animals held in the CADS data patch for the holding whose ear tags CADS did not return.
    /// The patch is best effort: if it cannot be read the CADS results are returned on their own.
    /// </summary>
    private async Task EnrichData(string cph, List<CattleResponse> results, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var earTag in await dataPatchStore.GetEarTagsAsync(cph, cancellationToken))
            {
                var rawData = await dataPatchStore.GetAnimalAsync(cph, earTag, cancellationToken);
                if (rawData is null)
                {
                    continue;
                }

                var patchData = ToCattleResponse(rawData);
                if (!rawData.IsAlive)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(patchData.EarTag))
                {
                    patchData.EarTag = earTag;
                }

                var sourceData = results.SingleOrDefault(x =>
                    string.Equals(x.EarTag, patchData.EarTag, StringComparison.OrdinalIgnoreCase));
                if (sourceData != null && rawData.IsAlive)
                {
                    results.Remove(sourceData);
                }

                results.Add(patchData);
            }
        }
        catch (AmazonServiceException ex)
        {
            LogCadsDataPatchUnavailable(ex, cph);
        }
    }

    private async Task<CadsAnimalsOnHolding> GetAnimalsPagedAsync(
        string cph,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            return await strategyFactory
                .BuildRestStrategy()
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Get animals on holding")
                .WithResourceUrl(AnimalsResource)
                .WithGet()
                .WithQueryParameter("CPH", cph)
                .WithQueryParameter("page", page.ToString(CultureInfo.InvariantCulture))
                .WithQueryParameter("pageSize", pageSize.ToString(CultureInfo.InvariantCulture))
                .Execute<CadsAnimalsOnHolding>();
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogHoldingNotKnownToCads(cph);
            throw new NotFoundException($"Holding '{cph}' was not found.");
        }
    }
}
