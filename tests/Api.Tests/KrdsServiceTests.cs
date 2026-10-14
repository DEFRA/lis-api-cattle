// <copyright file="KrdsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Lis.Api.Validation;
using Defra.Lis.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Text.Encoding;
using Convert = System.Convert;

public class KrdsServiceTests
{
    private const string OakfieldHolding = """
                                           {
                                             "identifier": "22/001/0001",
                                             "holdingType": "AH",
                                             "name": "Oakfield Farm",
                                             "startDate": "2010-04-01T00:00:00Z",
                                             "endDate": null,
                                             "location": {
                                               "osMapReference": "SJ4912",
                                               "easting": 349120,
                                               "northing": 312450,
                                               "address": {
                                                 "udprn": "12345678",
                                                 "addressLine1": "Oakfield Farm",
                                                 "addressLine2": "Church Lane",
                                                 "postTown": "Shrewsbury",
                                                 "locality": "Shropshire",
                                                 "postcode": "SY4 1AB",
                                                 "country": "England"
                                               }
                                             },
                                             "associations": [
                                               {
                                                 "customerNumber": "CUST0001",
                                                 "title": null,
                                                 "firstName": "Oakfield",
                                                 "lastName": "Farmer",
                                                 "name": "Oakfield Farmer",
                                                 "partyType": "Individual",
                                                 "email": "oakfield.farmer@oakhill-farms.co.uk",
                                                 "mobile": null,
                                                 "telephone": null,
                                                 "address": {
                                                   "addressLine1": "Oakfield Farm",
                                                   "addressLine2": "Church Lane",
                                                   "addressTown": "Shrewsbury",
                                                   "addressLocality": "Shropshire",
                                                   "addressNation": "England",
                                                   "addressPostcode": "SY4 1AB",
                                                   "addressCountryCode": "GBR"
                                                 },
                                                 "roles": [ { "code": "Keeper", "species": ["Cattle"] } ]
                                               }
                                             ],
                                             "allowedSpecies": ["Cattle"],
                                             "marks": [
                                               { "mark": "UK 324537", "startDate": "2010-04-01T00:00:00Z", "endDate": null, "species": ["Cattle"] },
                                               { "mark": "UK 111111", "startDate": "2000-01-01T00:00:00Z", "endDate": "2009-12-31T00:00:00Z", "species": ["Cattle"] }
                                             ]
                                           }
                                           """;

    private const string PaginatedHoldingsResponse = $$"""
                                                       {
                                                         "count": 1,
                                                         "totalCount": 10,
                                                         "values": [
                                                           {{OakfieldHolding}}
                                                         ],
                                                         "page": 2,
                                                         "pageSize": 1,
                                                         "totalPages": 10,
                                                         "hasNextPage": true,
                                                         "hasPreviousPage": true,
                                                         "nextCursor": "cursor123"
                                                       }
                                                       """;

    private const string OakfieldSubject = "0b6f2f0e-3c1a-4e8e-9d4b-2f6a1c9e7d51";

    // Duplicate CPH + role pairs are collapsed, as the fake's cph-associations route does.
    private const string OakfieldUserAccount = """
                                               {
                                                 "id": "7d7f4c2a-0000-4000-8000-000000000001",
                                                 "subject": "0b6f2f0e-3c1a-4e8e-9d4b-2f6a1c9e7d51",
                                                 "email": "oakfield.farmer@oakhill-farms.co.uk",
                                                 "firstName": "Oakfield",
                                                 "lastName": "Farmer",
                                                 "displayName": "Oakfield Farmer",
                                                 "cphAssociations": [
                                                   { "id": "a1", "cphNumber": "22/001/0001", "role": "Keeper", "partyId": null, "holdingId": "holding-0001", "holdingName": "Oakfield Farm" },
                                                   { "id": "a2", "cphNumber": "22/001/0001", "role": "Keeper", "partyId": null, "holdingId": "holding-0001", "holdingName": "Oakfield Farm" },
                                                   { "id": "a3", "cphNumber": "22/002/0002", "role": "Keeper", "partyId": null, "holdingId": null, "holdingName": null }
                                                 ],
                                                 "associationsRefreshedDate": "2026-09-29T09:00:00Z",
                                                 "lastUpdatedDate": "2026-09-29T09:00:00Z"
                                               }
                                               """;

    private const string TestClient = "krds-client";
    private const string TestPassphrase = "krds-passphrase";

    private static readonly KrdsApiOptions Options = new()
    {
        BaseUrl = "http://fake-service.test/krds",
        ClientId = TestClient,
        ClientSecret = TestPassphrase,
    };

    private readonly StubHttpMessageHandler handler = new();

    [Fact]
    public async Task GetHoldingAsync_CallsHoldingsEndpointWithBasicAuth()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldHolding);
        var service = CreateService();

        await service.GetHoldingAsync("22", "001", "0001", TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://fake-service.test/krds/api/v2/holdings/22/001/0001", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetHoldingAsync_MapsHoldingDetailsWithoutKeeperContactDetails()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldHolding);
        var service = CreateService();

        var holding = await service.GetHoldingAsync("22", "001", "0001", TestContext.Current.CancellationToken);

        Assert.Equal("22/001/0001", holding.Cph);
        Assert.Equal("Oakfield Farm", holding.Name);
        Assert.Equal("AH", holding.HoldingType);
        Assert.Equal(
            ["Oakfield Farm", "Church Lane", "Shrewsbury", "Shropshire", "SY4 1AB", "England"],
            holding.Address);
        Assert.Equal("Oakfield Farmer", holding.KeeperName);
        Assert.Single(holding.HerdMarks);
        Assert.Equal("UK 324537", holding.HerdMarks[0].Mark);
        Assert.Equal(DateTimeOffset.Parse("2010-04-01T00:00:00Z"), holding.HerdMarks[0].StartDate);
        Assert.Null(holding.HerdMarks[0].EndDate);
        Assert.Equal(["Cattle"], holding.HerdMarks[0].Species);
        Assert.Equal(["Cattle"], holding.AllowedSpecies);
        Assert.Single(holding.Associations);
        var association = holding.Associations[0];
        Assert.Equal("CUST0001", association.CustomerNumber);
        Assert.Equal("Oakfield", association.FirstName);
        Assert.Equal("Farmer", association.LastName);
        Assert.Equal("Oakfield Farmer", association.Name);
        Assert.Equal("Individual", association.PartyType);
        Assert.Equal("oakfield.farmer@oakhill-farms.co.uk", association.Email);
        Assert.Equal(
            ["Oakfield Farm", "Church Lane", "Shrewsbury", "Shropshire", "England", "SY4 1AB", "GBR"],
            association.Address);
        Assert.NotNull(association.Roles);
        Assert.Single(association.Roles);
        Assert.Equal("Keeper", association.Roles[0].Code);
    }

    [Fact]
    public async Task SearchHoldingsAsync_CallsHoldingsEndpointWithBasicAuthAndQueryParameters()
    {
        handler.RespondWith(HttpStatusCode.OK, PaginatedHoldingsResponse);
        var service = CreateService();

        var request = new HoldingSearchRequest(
            Search: "Oakfield",
            Page: 2,
            PageSize: 1,
            Sort: "asc",
            Order: "name");

        await service.SearchHoldingsAsync(request, TestContext.Current.CancellationToken);

        var sentRequest = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sentRequest.Method);
        Assert.Equal("Basic", sentRequest.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            sentRequest.Headers.Authorization.Parameter);
        Assert.Equal("/krds/api/v2/holdings", sentRequest.RequestUri!.AbsolutePath);

        var query = System.Web.HttpUtility.ParseQueryString(sentRequest.RequestUri.Query);
        Assert.Equal("Oakfield", query["search"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal("1", query["pageSize"]);
        Assert.Equal("asc", query["sort"]);
        Assert.Equal("name", query["order"]);
    }

    [Fact]
    public async Task SearchHoldingsAsync_MapsPaginatedHoldingsWithoutAssociations()
    {
        handler.RespondWith(HttpStatusCode.OK, PaginatedHoldingsResponse);
        var service = CreateService();

        var request = new HoldingSearchRequest(Search: "Oakfield");
        var result = await service.SearchHoldingsAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(10, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);

        var holding = Assert.Single(result.Items);
        Assert.Equal("22/001/0001", holding.Cph);
        Assert.Equal("Oakfield Farm", holding.Name);
        Assert.Equal("AH", holding.HoldingType);
        Assert.Equal(
            ["Oakfield Farm", "Church Lane", "Shrewsbury", "Shropshire", "SY4 1AB", "England"],
            holding.Address);
        Assert.Equal("Oakfield Farmer", holding.KeeperName);
        Assert.False(holding.GetType().GetProperty("Associations") is not null && holding is HoldingDetailedResponse);
        Assert.False(holding.GetType().GetProperty("HerdMarks") is not null && holding is HoldingDetailedResponse);
        Assert.False(holding.GetType().GetProperty("AllowedSpecies") is not null && holding is HoldingDetailedResponse);
    }

    [Fact]
    public async Task SearchHoldingsAsync_HandlesEmptyHoldingsResult()
    {
        handler.RespondWith(
            HttpStatusCode.OK,
            """{"count":0,"totalCount":0,"values":[],"page":1,"pageSize":10,"totalPages":0,"hasNextPage":false,"hasPreviousPage":false,"nextCursor":null}""");
        var service = CreateService();

        var result =
            await service.SearchHoldingsAsync(new HoldingSearchRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Count);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchHoldingsAsync_ThrowsArgumentException_WhenKrdsReturns400()
    {
        handler.RespondWith(HttpStatusCode.BadRequest, """{"title":"Bad Request","status":400}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchHoldingsAsync(new HoldingSearchRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHoldingAsync_HandlesHoldingWithoutAddressOrAssociations()
    {
        handler.RespondWith(
            HttpStatusCode.OK,
            """{"identifier":"22/099/0099","holdingType":"AH","name":null,"location":null,"associations":[],"allowedSpecies":[],"marks":[]}""");
        var service = CreateService();

        var holding = await service.GetHoldingAsync("22", "099", "0099", TestContext.Current.CancellationToken);

        Assert.Equal("22/099/0099", holding.Cph);
        Assert.Null(holding.Name);
        Assert.Empty(holding.Address);
        Assert.Null(holding.KeeperName);
        Assert.Empty(holding.HerdMarks);
        Assert.Empty(holding.AllowedSpecies);
    }

    [Fact]
    public async Task GetHoldingAsync_ThrowsNotFound_WhenKrdsReturns404()
    {
        handler.RespondWith(
            HttpStatusCode.NotFound,
            """{"title":"Not Found","status":404,"detail":"CPH not in the snapshot."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetHoldingAsync("22", "050", "0050", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHoldingAsync_ThrowsArgumentException_WhenKrdsReturns400()
    {
        handler.RespondWith(HttpStatusCode.BadRequest, """{"title":"Bad Request","status":400}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetHoldingAsync("22", "001", "0001", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2", "001", "0001")]
    [InlineData("22", "01", "0001")]
    [InlineData("22", "001", "001")]
    [InlineData("AB", "001", "0001")]
    [InlineData("", "001", "0001")]
    public async Task GetHoldingAsync_RejectsMalformedCphSegmentsWithoutCallingUpstream(
        string county,
        string parish,
        string holding)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetHoldingAsync(county, parish, holding, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetUserAccountAsync_CallsUserAccountsEndpointWithBasicAuth()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldUserAccount);
        var service = CreateService();

        await service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            $"http://fake-service.test/krds/api/v2/user-accounts/{OakfieldSubject}",
            request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetUserAccountAsync_PercentEncodesTheSubject()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldUserAccount);
        var service = CreateService();

        await service.GetUserAccountAsync("idp|user/1 2", TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/krds/api/v2/user-accounts/idp%7Cuser%2F1%202", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUserAccountAsync_MapsUserDetailsAndCphs()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldUserAccount);
        var service = CreateService();

        var user = await service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken);

        Assert.Equal(OakfieldSubject, user.Subject);
        Assert.Equal("oakfield.farmer@oakhill-farms.co.uk", user.Email);
        Assert.Equal("Oakfield", user.FirstName);
        Assert.Equal("Farmer", user.LastName);
        Assert.Equal("Oakfield Farmer", user.DisplayName);
        Assert.Collection(
            user.Cphs,
            cph =>
            {
                Assert.Equal("22/001/0001", cph.Cph);
                Assert.Equal("holding-0001", cph.HoldingId);
                Assert.Equal("Oakfield Farm", cph.HoldingName);
                Assert.Equal("Keeper", cph.Role);
            },
            cph =>
            {
                Assert.Equal("22/002/0002", cph.Cph);
                Assert.Null(cph.HoldingId);
                Assert.Null(cph.HoldingName);
                Assert.Equal("Keeper", cph.Role);
            });
    }

    [Fact]
    public async Task GetUserAccountAsync_HandlesAccountWithNullableFieldsUnset()
    {
        handler.RespondWith(
            HttpStatusCode.OK,
            $$"""{"id":"a1","subject":"{{OakfieldSubject}}","email":"new.keeper@example.com","firstName":null,"lastName":null,"displayName":null,"cphAssociations":[],"associationsRefreshedDate":null,"lastUpdatedDate":"2026-09-29T10:00:00Z"}""");
        var service = CreateService();

        var user = await service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken);

        Assert.Equal("new.keeper@example.com", user.Email);
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Null(user.DisplayName);
        Assert.Empty(user.Cphs);
    }

    [Fact]
    public async Task GetUserAccountAsync_ThrowsNotFound_WhenKrdsReturns404()
    {
        handler.RespondWith(
            HttpStatusCode.NotFound,
            """{"title":"Not Found","status":404,"detail":"The subject is not recognised."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetUserAccountAsync_ThrowsArgumentException_WhenKrdsReturns422()
    {
        handler.RespondWith(
            HttpStatusCode.UnprocessableEntity,
            """{"title":"Unprocessable Entity","status":422,"errors":{"subject":["subject must be a valid identity provider subject claim."]}}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetUserAccountAsync("not-a-uuid", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetUserAccountAsync_DoesNotTranslateUpstreamAuthOrServerFailuresIntoClientErrors(
        HttpStatusCode statusCode)
    {
        handler.RespondWith(statusCode, """{"status":0}""");
        var service = CreateService();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken));

        // Anything else falls through the exception handler as a 500, not a client error or the caller's own 401/403.
        Assert.False(exception is ArgumentException or NotFoundException or UnauthorizedAccessException);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserAccountAsync_RejectsBlankSubjectWithoutCallingUpstream(string subject)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetUserAccountAsync(subject, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EnsureUserAccountAsync_PostsTheClaimsToUserAccountsWithBasicAuth()
    {
        handler.RespondWith(HttpStatusCode.Created, OakfieldUserAccount);
        var service = CreateService();

        await service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://fake-service.test/krds/api/v2/user-accounts", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            request.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);
        Assert.Equal(OakfieldSubject, body.RootElement.GetProperty("sub").GetString());
        Assert.Equal("oakfield.farmer@oakhill-farms.co.uk", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("Oakfield", body.RootElement.GetProperty("given_name").GetString());
        Assert.Equal("Farmer", body.RootElement.GetProperty("family_name").GetString());
        Assert.Equal(4, body.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    public async Task EnsureUserAccountAsync_MapsTheReturnedAccount(HttpStatusCode statusCode)
    {
        handler.RespondWith(statusCode, OakfieldUserAccount);
        var service = CreateService();

        var user = await service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken);

        Assert.Equal(OakfieldSubject, user.Subject);
        Assert.Equal("oakfield.farmer@oakhill-farms.co.uk", user.Email);
        Assert.Equal("Oakfield Farmer", user.DisplayName);
        Assert.Equal(["22/001/0001", "22/002/0002"], user.Cphs.Select(cph => cph.Cph));
    }

    [Fact]
    public async Task EnsureUserAccountAsync_TrimsTheEmail()
    {
        handler.RespondWith(HttpStatusCode.OK, OakfieldUserAccount);
        var service = CreateService();
        var claims = OakfieldClaims();
        claims.Email = "  oakfield.farmer@oakhill-farms.co.uk ";

        await service.EnsureUserAccountAsync(claims, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);
        Assert.Equal("oakfield.farmer@oakhill-farms.co.uk", body.RootElement.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData(null, "oakfield.farmer@oakhill-farms.co.uk", "Oakfield", "Farmer")]
    [InlineData(OakfieldSubject, " ", "Oakfield", "Farmer")]
    [InlineData(OakfieldSubject, "oakfield.farmer@oakhill-farms.co.uk", "", "Farmer")]
    [InlineData(OakfieldSubject, "oakfield.farmer@oakhill-farms.co.uk", "Oakfield", null)]
    [InlineData(OakfieldSubject, "not-an-email", "Oakfield", "Farmer")]
    [InlineData(OakfieldSubject, "@oakhill-farms.co.uk", "Oakfield", "Farmer")]
    [InlineData(OakfieldSubject, "oakfield.farmer@", "Oakfield", "Farmer")]
    [InlineData(OakfieldSubject, "a@b@oakhill-farms.co.uk", "Oakfield", "Farmer")]
    public async Task EnsureUserAccountAsync_RejectsMissingOrInvalidClaimsWithoutCallingUpstream(
        string? sub,
        string? email,
        string? givenName,
        string? familyName)
    {
        var service = CreateService();
        var claims = new CreateUserRequest { Sub = sub, Email = email, GivenName = givenName, FamilyName = familyName };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureUserAccountAsync(claims, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EnsureUserAccountAsync_RejectsOverlongClaimsWithoutCallingUpstream()
    {
        var service = CreateService();
        var tooLongName = OakfieldClaims();
        tooLongName.GivenName = new string('a', 257);
        var tooLongEmail = OakfieldClaims();
        tooLongEmail.Email = new string('a', 301) + "@oakhill-farms.co.uk"; // 321 characters

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureUserAccountAsync(tooLongName, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureUserAccountAsync(tooLongEmail, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task EnsureUserAccountAsync_ThrowsArgumentException_WhenKrdsRejectsTheClaims(HttpStatusCode statusCode)
    {
        handler.RespondWith(
            statusCode,
            """{"title":"Unprocessable Entity","status":422,"errors":{"email":["email must not be null or blank."]}}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureUserAccountAsync_ThrowsConflict_WhenKrdsReturns409()
    {
        handler.RespondWith(
            HttpStatusCode.Conflict,
            """{"title":"Conflict","status":409,"detail":"The supplied email is already associated with a different account."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureUserAccountAsync_ThrowsServiceUnavailable_WhenKrdsReturns503()
    {
        handler.RespondWith(
            HttpStatusCode.ServiceUnavailable,
            """{"message":"CPH associations are not yet available. The SQLite read model cache is still loading."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task EnsureUserAccountAsync_DoesNotTranslateUpstreamAuthOrServerFailuresIntoClientErrors(
        HttpStatusCode statusCode)
    {
        handler.RespondWith(statusCode, """{"status":0}""");
        var service = CreateService();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.EnsureUserAccountAsync(OakfieldClaims(), TestContext.Current.CancellationToken));

        Assert.False(exception is ArgumentException or ConflictException or ServiceUnavailableException
            or UnauthorizedAccessException);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task SearchHoldingsAsync_ThrowsValidationException_WhenPagingParametersAreInvalid(
        int page,
        int pageSize)
    {
        var service = CreateService();
        var request = new HoldingSearchRequest(Page: page, PageSize: pageSize);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.SearchHoldingsAsync(request, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static CreateUserRequest OakfieldClaims() => new()
    {
        Sub = OakfieldSubject,
        Email = "oakfield.farmer@oakhill-farms.co.uk",
        GivenName = "Oakfield",
        FamilyName = "Farmer",
    };

    private KrdsService CreateService() => new(
        RestStrategyTestFactory.Create<KrdsService>(handler),
        new HoldingSearchRequestValidator(),
        Microsoft.Extensions.Options.Options.Create(Options),
        NullLogger<KrdsService>.Instance);
}
