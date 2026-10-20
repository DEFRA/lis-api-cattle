// <copyright file="KrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Globalization;
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
using Defra.Livestock.Sdk.Api.Strategies.Operations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads holding details and reads or ensures user accounts through the keeper-data-api (KRDS) V2 endpoints, using
/// a REST strategy.
/// </summary>
public sealed partial class KrdsService : IKrdsService
{
    private const string KeeperRoleCode = "Keeper";
    private const int MaxClaimLength = 256;
    private const int MaxEmailLength = 320;

    private readonly IRestStrategyFactory<KrdsService> strategyFactory;
    private readonly string? gatewayApiKey;
    private readonly IValidator<HoldingSearchRequest> holdingSearchRequestValidator;
    private readonly ILogger<KrdsService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KrdsService"/> class.
    /// Reads holding details and user accounts from the keeper-data-api (KRDS) V2 endpoints through a REST strategy.
    /// </summary>
    /// <param name="strategyFactory">The REST strategy factory used to create API requests.</param>
    /// <param name="holdingSearchRequestValidator">The validator for holding search requests.</param>
    /// <param name="options">The KRDS API configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public KrdsService(
        IRestStrategyFactory<KrdsService> strategyFactory,
        IValidator<HoldingSearchRequest> holdingSearchRequestValidator,
        IOptions<KrdsApiOptions> options,
        ILogger<KrdsService> logger)
    {
        this.strategyFactory = strategyFactory;
        gatewayApiKey = options.Value.GatewayApiKey;
        this.holdingSearchRequestValidator = holdingSearchRequestValidator;
        this.logger = logger;

        strategyFactory
            .WithDefaultLogger(this.logger)
            .WithDefaultApiDescription("KRDS API")
            .WithDefaultBaseUrl(options.Value.BaseUrl)
            .WithDefaultBasicAuth(options.Value.ClientId, options.Value.ClientSecret)
            .WithDefaultJsonSerializerOptions(UpstreamJson.Options);
    }

    public async Task<HoldingDetailedResponse> GetHoldingAsync(
        string county,
        string parish,
        string holding,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(county, CountySegment(), nameof(county), "2 digits");
        ValidateSegment(parish, ParishSegment(), nameof(parish), "3 digits");
        ValidateSegment(holding, HoldingSegment(), nameof(holding), "4 digits");

        var cph = $"{county}/{parish}/{holding}";

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithGatewayApiKey(gatewayApiKey)
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Get holding details")
                .WithResourceUrl($"api/v2/holdings/{county}/{parish}/{holding}")
                .WithGet()
                .ExecuteAndTransform<KrdsHolding, HoldingDetailedResponse>(krdsHolding =>
                    ToHoldingDetailedResponse(cph, krdsHolding));

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

    public async Task<PaginatedResponse<HoldingResponse>> SearchHoldingsAsync(
        HoldingSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithGatewayApiKey(gatewayApiKey)
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Search holdings")
                .WithRequestValidation(() => holdingSearchRequestValidator.ValidateAsync(request, cancellationToken))
                .WithResourceUrl("api/v2/holdings")
                .WithGet()
                .WithQueryParameter(() => !string.IsNullOrWhiteSpace(request.Search), "search", request.Search!)
                .WithQueryParameter(
                    () => request.Page.HasValue,
                    "page",
                    request.Page?.ToString(CultureInfo.InvariantCulture)!)
                .WithQueryParameter(
                    () => request.PageSize.HasValue,
                    "pageSize",
                    request.PageSize?.ToString(CultureInfo.InvariantCulture)!)
                .WithQueryParameter(() => !string.IsNullOrWhiteSpace(request.Sort), "sort", request.Sort!)
                .WithQueryParameter(() => !string.IsNullOrWhiteSpace(request.Order), "order", request.Order!)
                .ExecuteAndTransform<KrdsPaginatedResult<KrdsHolding>, PaginatedResponse<HoldingResponse>>(
                    ToPaginatedHoldingsResponse);

            LogRetrievedHoldings(response.Items.Count, response.Page, response.TotalPages);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex))
        {
            throw new ArgumentException("Invalid search parameters provided to KRDS.", ex);
        }
    }

    public async Task<UserDetailsResponse> GetUserAccountAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        // The V2 contract treats the subject as an opaque identity provider claim, so only blank values are rejected here.
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("A subject is required.", nameof(subject));
        }

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithGatewayApiKey(gatewayApiKey)
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Get user account")
                .WithResourceUrl($"api/v2/user-accounts/{Uri.EscapeDataString(subject)}")
                .WithGet()
                .ExecuteAndTransform<KrdsUserAccount, UserDetailsResponse>(account =>
                    ToUserDetailsResponse(subject, account));

            LogRetrievedUserAccount(response.Cphs.Count);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsNotFound(ex))
        {
            LogUserAccountNotKnownToKrds();
            throw new NotFoundException($"User '{subject}' was not found.");
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex) ||
                                               UpstreamErrors.IsUnprocessableEntity(ex))
        {
            throw new ArgumentException($"User '{subject}' is not a valid subject.", nameof(subject), ex);
        }
    }

    public async Task<UserDetailsResponse> EnsureUserAccountAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = ToEnsureUserAccountRequest(request);

        try
        {
            var response = await strategyFactory
                .BuildRestStrategy()
                .WithGatewayApiKey(gatewayApiKey)
                .WithCancellationToken(cancellationToken)
                .WithActionDescription("Ensure user account")
                .WithResourceUrl("api/v2/user-accounts")
                .WithPost()
                .WithPayload(payload)
                .ExecuteAndTransform<KrdsUserAccount, UserDetailsResponse>(account =>
                    ToUserDetailsResponse(payload.Sub, account));

            LogEnsuredUserAccount(response.Cphs.Count);

            return response;
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsBadRequest(ex) ||
                                               UpstreamErrors.IsUnprocessableEntity(ex))
        {
            throw new ArgumentException(
                $"The user account for subject '{payload.Sub}' was rejected by KRDS.",
                nameof(request),
                ex);
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsConflict(ex))
        {
            LogUserAccountConflict();
            throw new ConflictException("The email address is already associated with a different user account.");
        }
        catch (RestResponseException ex) when (UpstreamErrors.IsServiceUnavailable(ex))
        {
            LogKrdsUnavailable("Ensure user account");
            throw new ServiceUnavailableException("KRDS is temporarily unavailable, try again later.", ex);
        }
    }

    private static KrdsEnsureUserAccountRequest ToEnsureUserAccountRequest(CreateUserRequest? request)
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

    private static UserDetailsResponse ToUserDetailsResponse(string subject, KrdsUserAccount account)
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

    private static HoldingResponse ToHoldingResponse(string cph, KrdsHolding holding)
    {
        ArgumentNullException.ThrowIfNull(holding);

        var address = holding.Location?.Address;

        return new HoldingResponse
        {
            Cph = string.IsNullOrWhiteSpace(holding.Identifier) ? cph : holding.Identifier,
            Name = holding.Name,
            HoldingType = holding.HoldingType,
            StartDate = holding.StartDate,
            EndDate = holding.EndDate,
            Active = IsHoldingActive(holding),
            Address =
            [
                .. new[]
                    {
                        address?.AddressLine1, address?.AddressLine2, address?.PostTown, address?.Locality,
                        address?.Postcode, address?.Country,
                    }
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line!.Trim())
            ],
            KeeperName = holding.Associations?
                .FirstOrDefault(association => association.Roles?.Any(role =>
                    string.Equals(role.Code, KeeperRoleCode, StringComparison.OrdinalIgnoreCase)) == true)?
                .Name,
        };
    }

    private static bool IsHoldingActive(KrdsHolding holding)
    {
        var utcNow = DateTimeOffset.UtcNow;

        if (holding.StartDate is not null && utcNow < holding.StartDate.Value.ToUniversalTime())
        {
            return false;
        }

        if (holding.EndDate is not null && utcNow > holding.EndDate.Value.ToUniversalTime())
        {
            return false;
        }

        return true;
    }

    private static HoldingDetailedResponse ToHoldingDetailedResponse(string cph, KrdsHolding holding)
    {
        var baseResponse = ToHoldingResponse(cph, holding);

        return new HoldingDetailedResponse
        {
            Cph = baseResponse.Cph,
            Name = baseResponse.Name,
            HoldingType = baseResponse.HoldingType,
            StartDate = baseResponse.StartDate,
            EndDate = baseResponse.EndDate,
            Active = baseResponse.Active,
            Address = baseResponse.Address,
            KeeperName = baseResponse.KeeperName,
            HerdMarks =
            [
                .. (holding.Marks ?? [])
                .Where(mark => mark.EndDate is null && !string.IsNullOrWhiteSpace(mark.Mark))
                .Select(mark => new HoldingMarkResponse
                {
                    Mark = mark.Mark, StartDate = mark.StartDate, EndDate = mark.EndDate, Species = mark.Species ?? [],
                })
            ],
            AllowedSpecies =
                (holding.AllowedSpecies ?? []).Where(species => !string.IsNullOrWhiteSpace(species)).ToList(),
            Associations =
            [
                .. (holding.Associations ?? [])
                .Select(association => new HoldingAssociationResponse
                {
                    CustomerNumber = association.CustomerNumber,
                    Title = association.Title,
                    FirstName = association.FirstName,
                    LastName = association.LastName,
                    Name = association.Name,
                    PartyType = association.PartyType,
                    Email = association.Email,
                    Mobile = association.Mobile,
                    Telephone = association.Telephone,
                    Address =
                    [
                        .. new[]
                            {
                                association.Address?.AddressLine1, association.Address?.AddressLine2,
                                association.Address?.AddressTown, association.Address?.AddressLocality,
                                association.Address?.AddressNation, association.Address?.AddressPostcode,
                                association.Address?.AddressCountryCode,
                            }
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line!.Trim())
                    ],
                    Roles =
                    [
                        .. (association.Roles ?? [])
                        .Select(role => new HoldingRoleResponse { Code = role.Code, Species = role.Species ?? [], })
                    ],
                })
            ],
        };
    }

    private static PaginatedResponse<HoldingResponse> ToPaginatedHoldingsResponse(
        KrdsPaginatedResult<KrdsHolding> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new PaginatedResponse<HoldingResponse>
        {
            Count = result.Count,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasNextPage = result.HasNextPage,
            HasPreviousPage = result.HasPreviousPage,
            Items =
            [
                .. (result.Values ?? [])
                .Select(holding => ToHoldingResponse(holding.Identifier ?? string.Empty, holding))
            ],
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
