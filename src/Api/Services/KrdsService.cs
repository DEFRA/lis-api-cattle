// <copyright file="KrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Text.RegularExpressions;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Krds;
using Defra.Lis.Api.Services.Upstream;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads holding details from the keeper-data-api (KRDS) V2 holdings endpoint through a REST strategy.
/// </summary>
public sealed partial class KrdsService(
    IRestStrategyFactory<KrdsService> strategyFactory,
    IOptions<KrdsApiOptions> options,
    ILogger<KrdsService> logger)
    : IKrdsService
{
    private const string ApiDescription = "KRDS holdings API";
    private const string KeeperRoleCode = "Keeper";

    public async Task<HoldingResponse> GetHoldingAsync(string county, string parish, string holding, CancellationToken cancellationToken = default)
    {
        ValidateSegment(county, CountySegment(), nameof(county), "2 digits");
        ValidateSegment(parish, ParishSegment(), nameof(parish), "3 digits");
        ValidateSegment(holding, HoldingSegment(), nameof(holding), "4 digits");

        var cph = $"{county}/{parish}/{holding}";
        var settings = options.Value;

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithLogger(logger)
                .WithCancellationToken(cancellationToken)
                .WithApiDescription(ApiDescription)
                .WithActionDescription("Get holding details")
                .WithBaseUrl(settings.BaseUrl)
                .WithResourceUrl($"krds/api/v2/holdings/{county}/{parish}/{holding}")
                .WithHeader(BasicAuthorization.HeaderName, BasicAuthorization.HeaderValue(settings.ClientId, settings.ClientSecret))
                .WithJsonSerializerOptions(UpstreamJson.Options)
                .WithGet()
                .ExecuteAndTransform<KrdsHolding, HoldingResponse>(krdsHolding => ToHoldingResponse(cph, krdsHolding));

            LogRetrievedHoldingDetails(cph);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogHoldingNotKnownToKrds(cph);
            throw new NotFoundException($"Holding '{cph}' was not found.");
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex))
        {
            throw new ArgumentException($"Holding '{cph}' is not a valid CPH.", ex);
        }
    }

    internal static HoldingResponse ToHoldingResponse(string cph, KrdsHolding holding)
    {
        ArgumentNullException.ThrowIfNull(holding);

        var address = holding.Location?.Address;

        return new HoldingResponse
        {
            Cph = string.IsNullOrWhiteSpace(holding.Identifier) ? cph : holding.Identifier,
            Name = holding.Name,
            HoldingType = holding.HoldingType,
            Address = new[]
                {
                    address?.AddressLine1,
                    address?.AddressLine2,
                    address?.PostTown,
                    address?.Locality,
                    address?.Postcode,
                    address?.Country,
                }
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line!.Trim())
                .ToList(),
            KeeperName = holding.Associations?
                .FirstOrDefault(association => association.Roles?.Any(role =>
                    string.Equals(role.Code, KeeperRoleCode, StringComparison.OrdinalIgnoreCase)) == true)?
                .Name,
            HerdMarks = (holding.Marks ?? [])
                .Where(mark => mark.EndDate is null && !string.IsNullOrWhiteSpace(mark.Mark))
                .Select(mark => mark.Mark!)
                .ToList(),
            AllowedSpecies = (holding.AllowedSpecies ?? []).Where(species => !string.IsNullOrWhiteSpace(species)).ToList(),
        };
    }

    [GeneratedRegex("^[0-9]{2}$")]
    private static partial Regex CountySegment();

    [GeneratedRegex("^[0-9]{3}$")]
    private static partial Regex ParishSegment();

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex HoldingSegment();

    private static void ValidateSegment(string value, Regex pattern, string name, string expected)
    {
        if (string.IsNullOrWhiteSpace(value) || !pattern.IsMatch(value))
        {
            throw new ArgumentException($"CPH segment '{name}' must be {expected}.", name);
        }
    }
}
