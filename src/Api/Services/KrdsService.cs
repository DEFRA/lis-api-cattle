// <copyright file="KrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Text.RegularExpressions;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Krds;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Services.Upstream;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads holding details and reads or ensures user accounts through the keeper-data-api (KRDS) V2 endpoints, using
/// a REST strategy.
/// </summary>
public sealed partial class KrdsService(
    IRestStrategyFactory<KrdsService> strategyFactory,
    IOptions<KrdsApiOptions> options,
    ILogger<KrdsService> logger)
    : IKrdsService
{
    private const string ApiDescription = "KRDS API";
    private const string KeeperRoleCode = "Keeper";
    private const int MaxClaimLength = 256;
    private const int MaxEmailLength = 320;

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
                .WithResourceUrl($"api/v2/holdings/{county}/{parish}/{holding}")
                .WithBasicAuth(settings.ClientId, settings.ClientSecret)
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

    public async Task<UserDetailsResponse> GetUserAccountAsync(string subject, CancellationToken cancellationToken = default)
    {
        // The V2 contract treats the subject as an opaque identity provider claim, so only blank values are rejected here.
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("A subject is required.", nameof(subject));
        }

        var settings = options.Value;

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithLogger(logger)
                .WithCancellationToken(cancellationToken)
                .WithApiDescription(ApiDescription)
                .WithActionDescription("Get user account")
                .WithBaseUrl(settings.BaseUrl)
                .WithResourceUrl($"api/v2/user-accounts/{Uri.EscapeDataString(subject)}")
                .WithBasicAuth(settings.ClientId, settings.ClientSecret)
                .WithJsonSerializerOptions(UpstreamJson.Options)
                .WithGet()
                .ExecuteAndTransform<KrdsUserAccount, UserDetailsResponse>(account => ToUserDetailsResponse(subject, account));

            LogRetrievedUserAccount(subject, response.Cphs.Count);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogUserAccountNotKnownToKrds(subject);
            throw new NotFoundException($"User '{subject}' was not found.");
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex) || UpstreamErrors.IsUnprocessableEntity(ex))
        {
            throw new ArgumentException($"User '{subject}' is not a valid subject.", nameof(subject), ex);
        }
    }

    public async Task<UserDetailsResponse> EnsureUserAccountAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var payload = ToEnsureUserAccountRequest(request);
        var settings = options.Value;

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithLogger(logger)
                .WithCancellationToken(cancellationToken)
                .WithApiDescription(ApiDescription)
                .WithActionDescription("Ensure user account")
                .WithBaseUrl(settings.BaseUrl)
                .WithResourceUrl("api/v2/user-accounts")
                .WithBasicAuth(settings.ClientId, settings.ClientSecret)
                .WithJsonSerializerOptions(UpstreamJson.Options)
                .WithPost()
                .WithPayload(payload)
                .ExecuteAndTransform<KrdsUserAccount, UserDetailsResponse>(account => ToUserDetailsResponse(payload.Sub, account));

            LogEnsuredUserAccount(payload.Sub, response.Cphs.Count);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex) || UpstreamErrors.IsUnprocessableEntity(ex))
        {
            throw new ArgumentException($"The user account for subject '{payload.Sub}' was rejected by KRDS.", nameof(request), ex);
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsConflict(ex))
        {
            LogUserAccountConflict(payload.Sub);
            throw new ConflictException("The email address is already associated with a different user account.");
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsServiceUnavailable(ex))
        {
            LogKrdsUnavailable("Ensure user account");
            throw new ServiceUnavailableException("KRDS is temporarily unavailable, try again later.", ex);
        }
    }

    internal static KrdsEnsureUserAccountRequest ToEnsureUserAccountRequest(CreateUserRequest? request)
    {
        // Mirrors the keeper-data-api validation so a bad request fails here with a 400, without an upstream call.
        // The messages name the field only, never its value, as the claims are personal data.
        if (request is null)
        {
            throw new ArgumentException("A request body is required.", nameof(request));
        }

        var sub = RequireClaim(request.Sub, "sub", MaxClaimLength);
        var email = RequireClaim(request.Email?.Trim(), "email", MaxEmailLength);
        var givenName = RequireClaim(request.GivenName, "given_name", MaxClaimLength);
        var familyName = RequireClaim(request.FamilyName, "family_name", MaxClaimLength);

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            throw new ArgumentException("Claim 'email' must be a valid email address.", nameof(request));
        }

        return new KrdsEnsureUserAccountRequest(sub, email, givenName, familyName);
    }

    internal static UserDetailsResponse ToUserDetailsResponse(string subject, KrdsUserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new UserDetailsResponse
        {
            Subject = string.IsNullOrWhiteSpace(account.Subject) ? subject : account.Subject,
            Email = account.Email ?? string.Empty,
            FirstName = account.FirstName,
            LastName = account.LastName,
            DisplayName = account.DisplayName,
            Cphs = (account.CphAssociations ?? [])
                .Where(association => !string.IsNullOrWhiteSpace(association.CphNumber))
                .DistinctBy(association => (association.CphNumber, association.Role))
                .Select(association => new UserCphResponse
                {
                    Cph = association.CphNumber!,
                    HoldingId = association.HoldingId,
                    HoldingName = association.HoldingName,
                    Role = association.Role ?? string.Empty,
                })
                .ToList(),
        };
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

    private static string RequireClaim(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Claim '{name}' is required.", name);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException($"Claim '{name}' must be {maxLength} characters or fewer.", name);
        }

        return value;
    }

    private static void ValidateSegment(string value, Regex pattern, string name, string expected)
    {
        if (string.IsNullOrWhiteSpace(value) || !pattern.IsMatch(value))
        {
            throw new ArgumentException($"CPH segment '{name}' must be {expected}.", name);
        }
    }
}
