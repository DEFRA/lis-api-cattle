// <copyright file="CadsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class CadsServiceTests
{
    private const string Cph = "22/001/0001";
    private const string TestClient = "cads-client";
    private const string TestPassphrase = "cads-passphrase";

    private static readonly CadsApiOptions Options = new()
    {
        BaseUrl = "http://fake-service.test/cads/",
        ClientId = TestClient,
        ClientSecret = TestPassphrase,
        PageSize = 2,
    };

    private readonly StubHttpMessageHandler handler = new();

    [Fact]
    public async Task GetCattleByCphAsync_CallsAnimalsEndpointWithCphPagingAndBasicAuth()
    {
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalPages: 1, hasNextPage: false));
        var service = CreateService();

        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ToList();

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://fake-service.test/cads/api/v1/bovine/animals?CPH=22%2F001%2F0001&page=1&pageSize=2", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{TestClient}:{TestPassphrase}")), request.Headers.Authorization.Parameter);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_MapsCadsAnimalToCattleResponse()
    {
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalPages: 1, hasNextPage: false));
        var service = CreateService();

        var animal = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).Single();

        Assert.Equal("UK200000000001", animal.EarTag);
        Assert.Equal(new DateOnly(2023, 2, 1), animal.DateBirth);
        Assert.Equal(new DateOnly(2023, 2, 15), animal.DateOnCph);
        Assert.Equal("Male", animal.Sex);
        Assert.Equal("AA", animal.BreedCode);
        Assert.Equal("Aberdeen Angus", animal.BreedName);
        Assert.Equal("Aberdeen Angus", animal.Breed);
        Assert.Equal("Alive", animal.Status);
        Assert.Empty(animal.Errors);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReadsEveryPageAndExcludesDeadAnimals()
    {
        handler
            .RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive"), Animal("UK200000000002", "Dead")], page: 1, totalPages: 2, hasNextPage: true))
            .RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000003", "Alive")], page: 2, totalPages: 2, hasNextPage: false));
        var service = CreateService();

        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).Select(c => c.EarTag).ToList();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("page=1&pageSize=2", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2&pageSize=2", handler.Requests[1].RequestUri!.Query);
        Assert.Equal(["UK200000000001", "UK200000000003"], result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsEmpty_WhenHoldingHasNoAnimals()
    {
        handler.RespondWith(HttpStatusCode.OK, Page([], page: 1, totalPages: 0, hasNextPage: false));
        var service = CreateService();

        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ThrowsNotFound_WhenCadsReturns404()
    {
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleByCphAsync_PropagatesOtherUpstreamFailures()
    {
        handler.RespondWith(HttpStatusCode.Unauthorized, """{"title":"Unauthorized","status":401}""");
        var service = CreateService();

        await Assert.ThrowsAsync<RestResponseException>(() => service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleByCphAsync_RejectsBlankCph()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetCattleByCphAsync(" ", TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static string Animal(string earTag, string status) => $$"""
        {
          "identifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "{{earTag}}" },
          "birthDate": "2023-02-01",
          "dateOnCPH": "2023-02-15",
          "dateOffCPH": null,
          "species": "Cattle",
          "sex": "Male",
          "breedCode": { "schema": "cts.breed", "breedName": "Aberdeen Angus", "identifier": "AA" },
          "status": "{{status}}"
        }
        """;

    private static string Page(string[] animals, int page, int totalPages, bool hasNextPage) => $$"""
        {
          "results": [{{string.Join(",", animals)}}],
          "count": {{animals.Length}},
          "totalCount": {{animals.Length}},
          "page": {{page}},
          "pageSize": 2,
          "totalPages": {{totalPages}},
          "hasNextPage": {{hasNextPage.ToString().ToLowerInvariant()}},
          "hasPreviousPage": {{(page > 1).ToString().ToLowerInvariant()}}
        }
        """;

    private CadsService CreateService() => new(
        RestStrategyTestFactory.Create<CadsService>(handler),
        Microsoft.Extensions.Options.Options.Create(Options),
        NullLogger<CadsService>.Instance);
}
