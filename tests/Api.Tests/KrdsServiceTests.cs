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

    private const string TestClient = "krds-client";
    private const string TestPassphrase = "krds-passphrase";

    private static readonly KrdsApiOptions Options = new()
    {
        BaseUrl = "http://fake-service.test",
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

    private KrdsService CreateService() => new(
        RestStrategyTestFactory.Create<KrdsService>(handler),
        Microsoft.Extensions.Options.Options.Create(Options),
        NullLogger<KrdsService>.Instance);
}
