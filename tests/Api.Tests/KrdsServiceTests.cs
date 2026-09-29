// <copyright file="KrdsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Lis.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

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
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{TestClient}:{TestPassphrase}")), request.Headers.Authorization.Parameter);
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
        Assert.Equal(["Oakfield Farm", "Church Lane", "Shrewsbury", "Shropshire", "SY4 1AB", "England"], holding.Address);
        Assert.Equal("Oakfield Farmer", holding.KeeperName);
        Assert.Equal(["UK 324537"], holding.HerdMarks);
        Assert.Equal(["Cattle"], holding.AllowedSpecies);
    }

    [Fact]
    public async Task GetHoldingAsync_HandlesHoldingWithoutAddressOrAssociations()
    {
        handler.RespondWith(HttpStatusCode.OK, """{"identifier":"22/099/0099","holdingType":"AH","name":null,"location":null,"associations":[],"allowedSpecies":[],"marks":[]}""");
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
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404,"detail":"CPH not in the snapshot."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetHoldingAsync("22", "050", "0050", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHoldingAsync_ThrowsArgumentException_WhenKrdsReturns400()
    {
        handler.RespondWith(HttpStatusCode.BadRequest, """{"title":"Bad Request","status":400}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetHoldingAsync("22", "001", "0001", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2", "001", "0001")]
    [InlineData("22", "01", "0001")]
    [InlineData("22", "001", "001")]
    [InlineData("AB", "001", "0001")]
    [InlineData("", "001", "0001")]
    public async Task GetHoldingAsync_RejectsMalformedCphSegmentsWithoutCallingUpstream(string county, string parish, string holding)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetHoldingAsync(county, parish, holding, TestContext.Current.CancellationToken));
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
        Assert.Equal($"http://fake-service.test/krds/api/v2/user-accounts/{OakfieldSubject}", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{TestClient}:{TestPassphrase}")), request.Headers.Authorization.Parameter);
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
        handler.RespondWith(HttpStatusCode.OK, $$"""{"id":"a1","subject":"{{OakfieldSubject}}","email":"new.keeper@example.com","firstName":null,"lastName":null,"displayName":null,"cphAssociations":[],"associationsRefreshedDate":null,"lastUpdatedDate":"2026-09-29T10:00:00Z"}""");
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
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404,"detail":"The subject is not recognised."}""");
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetUserAccountAsync_ThrowsArgumentException_WhenKrdsReturns422()
    {
        handler.RespondWith(HttpStatusCode.UnprocessableEntity, """{"title":"Unprocessable Entity","status":422,"errors":{"subject":["subject must be a valid identity provider subject claim."]}}""");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserAccountAsync("not-a-uuid", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetUserAccountAsync_DoesNotTranslateUpstreamAuthOrServerFailuresIntoClientErrors(HttpStatusCode statusCode)
    {
        handler.RespondWith(statusCode, """{"status":0}""");
        var service = CreateService();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => service.GetUserAccountAsync(OakfieldSubject, TestContext.Current.CancellationToken));

        // Anything else falls through the exception handler as a 500, not a client error or the caller's own 401/403.
        Assert.False(exception is ArgumentException or NotFoundException or UnauthorizedAccessException);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserAccountAsync_RejectsBlankSubjectWithoutCallingUpstream(string subject)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserAccountAsync(subject, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private KrdsService CreateService() => new(
        RestStrategyTestFactory.Create<KrdsService>(handler),
        Microsoft.Extensions.Options.Options.Create(Options),
        NullLogger<KrdsService>.Instance);
}
