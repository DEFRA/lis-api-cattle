// <copyright file="CadsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using Amazon.S3;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Resilience.CircuitBreaker;
using Defra.Livestock.Sdk.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public sealed class CadsServiceTests : IDisposable
{
    private const string Cph = "22/001/0001";
    private const string EarTag = "UK200000000001";
    private const string TestClient = "cads-client";
    private const string TestPassphrase = "cads-passphrase";
    private const string TestGatewayKey = "cads-gateway-key";

    private static readonly CadsApiOptions Options = new()
    {
        BaseUrl = "http://fake-service.test/cads/",
        ClientId = TestClient,
        ClientSecret = TestPassphrase,
        PageSize = 2,
        CircuitBreakerConsecutiveFailureThreshold = 5,
    };

    private static readonly CadsDataPatchOptions DataPatchOptions = new() { BucketName = "cads-data-patches", };

    private readonly StubHttpMessageHandler handler = new();
    private readonly Mock<ICadsDataPatchStore> dataPatchStore = new();

    public CadsServiceTests()
    {
        // No patch files unless a test sets some up.
        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task GetCattleByCphAsync_CallsAnimalsEndpointWithCphPagingAndBasicAuth()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ToList();

        // Assert
        var item = Assert.Single(handler.Requests);

        Assert.Equal(HttpMethod.Get, item.Method);

        Assert.Equal(
            "http://fake-service.test/cads/api/v1/bovine/animals?CPH=22%2F001%2F0001&page=1&pageSize=2",
            item.RequestUri!.ToString());

        Assert.Equal("Basic", item.Headers.Authorization!.Scheme);

        Assert.Equal(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            item.Headers.Authorization.Parameter);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_MapsCadsAnimalToCattleResponse()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var service = CreateService();

        // Act
        var animal = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).Single();

        // Assert
        Assert.Equal("UK200000000001", animal.EarTag);
        Assert.Equal(new DateOnly(2023, 2, 1), animal.DateBirth);
        Assert.Equal(new DateOnly(2023, 2, 15), animal.DateOnCph);
        Assert.Equal("Male", animal.Sex);
        Assert.Equal("AA", animal.BreedCode);
        Assert.Equal("Aberdeen Angus", animal.BreedName);
        Assert.Equal("Aberdeen Angus", animal.Breed);
        Assert.Equal("Passport Produced", animal.Status);
        Assert.Empty(animal.Errors);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReadsEveryPageUntilTotalRecordsAreReached()
    {
        // Arrange
        handler
            .RespondWith(
                HttpStatusCode.OK,
                Page(
                    [Animal("UK200000000001", "Passport Produced"), Animal("UK200000000002", "Passport Produced")],
                    page: 1,
                    totalRecords: 3))
            .RespondWith(
                HttpStatusCode.OK,
                Page([Animal("UK200000000003", "Passport Produced")], page: 2, totalRecords: 3));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken))
            .Select(c => c.EarTag).ToList();

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("page=1&pageSize=2", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2&pageSize=2", handler.Requests[1].RequestUri!.Query);
        Assert.Equal(["UK200000000001", "UK200000000002", "UK200000000003"], result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_StopsWhenPageTimesPageSizeReachesTotalRecords()
    {
        // Arrange
        handler
            .RespondWith(
                HttpStatusCode.OK,
                Page(
                    [Animal("UK200000000001", "Passport Produced"), Animal("UK200000000002", "Passport Produced")],
                    page: 1,
                    totalRecords: 4))
            .RespondWith(
                HttpStatusCode.OK,
                Page(
                    [Animal("UK200000000003", "Passport Produced"), Animal("UK200000000004", "Passport Produced")],
                    page: 2,
                    totalRecords: 4));

        var service = CreateService();

        // Act
        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(4, result.Count());
    }

    [Fact]
    public async Task GetCattleByCphAsync_StopsOnAnEmptyPage()
    {
        // Arrange
        handler
            .RespondWith(
                HttpStatusCode.OK,
                Page(
                    [Animal("UK200000000001", "Passport Produced"), Animal("UK200000000002", "Passport Produced")],
                    page: 1,
                    totalRecords: 10))
            .RespondWith(HttpStatusCode.OK, Page([], page: 2, totalRecords: 10));

        var service = CreateService();

        // Act
        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetCattleByCphAsync_StopsAtMaxPages()
    {
        // Arrange
        for (var i = 0; i < 101; i++)
        {
            handler.RespondWith(
                HttpStatusCode.OK,
                Page([Animal("UK200000000001", "Passport Produced")], page: i + 1, totalRecords: 1_000_000));
        }

        var service = CreateService();

        // Act
        await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100, handler.Requests.Count);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsEveryAnimalWithoutFilteringOnStatus()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page(
                [Animal("UK200000000001", "Passport Produced"), Animal("UK200000000002", "Registered")],
                page: 1,
                totalRecords: 2));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.Equal(["Passport Produced", "Registered"], result.Select(c => c.Status));
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsEmpty_WhenHoldingHasNoAnimals()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([], page: 1, totalRecords: 0));

        var service = CreateService();

        // Act
        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ThrowsNotFound_WhenCadsReturns404()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleByCphAsync_PropagatesOtherUpstreamFailures()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.Unauthorized, """{"title":"Unauthorized","status":401}""");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleByCphAsync_RejectsBlankCph()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetCattleByCphAsync(" ", TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetCattleByCphAsync_AddsLiveAnimalsFromTheDataPatchThatCadsDidNotReturn()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalRecords: 1));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UK200000000001", "UK200000000005"]);
        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchAnimal("UK200000000001", "Alive"));
        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000005", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchAnimal("UK200000000005", "Alive"));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.Equal(["UK200000000001", "UK200000000005"], result.Select(c => c.EarTag));
        Assert.Equal("Aberdeen Angus", result[1].BreedName);
    }

    [Fact]
    public async Task GetCattleByCphAsync_OverwritesSourceDataWithValuesFromPatchData()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalRecords: 1));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UK200000000001"]);

        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000001", It.IsAny<CancellationToken>())).ReturnsAsync(
            new CadsAnimal(
                new CadsIdentifier("uk.gov.defra.ear-tag.conventional", "UK200000000001"),
                new DateOnly(2022, 1, 1),
                new DateOnly(2022, 2, 1),
                null,
                "Cattle",
                "Female",
                new CadsBreedCode("cts.breed", "Highland", "HL"),
                "Alive"));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ToList();

        // Assert
        var animal = Assert.Single(result);

        Assert.Equal("UK200000000001", animal.EarTag);
        Assert.Equal(new DateOnly(2022, 1, 1), animal.DateBirth);
        Assert.Equal(new DateOnly(2022, 2, 1), animal.DateOnCph);
        Assert.Equal("Female", animal.Sex);
        Assert.Equal("HL", animal.BreedCode);
        Assert.Equal("Highland", animal.BreedName);
        Assert.Equal("Highland", animal.Breed);
        Assert.Equal("Alive", animal.Status);
    }

    [Fact]
    public async Task GetCattleByCphAsync_DoesNotPatchInAnAnimalCadsReturnsOnALaterPage()
    {
        // Arrange
        handler
            .RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalRecords: 3))
            .RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000003", "Alive")], page: 2, totalRecords: 3));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["uk200000000003"]);
        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "uk200000000003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchAnimal("uk200000000003", "Alive"));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken))
            .Select(c => c.EarTag).ToList();

        // Assert
        Assert.Equal(["UK200000000001", "uk200000000003"], result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_SkipsPatchedAnimalsThatAreNotAliveOrUnreadable()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([], page: 1, totalRecords: 0));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UK200000000006", "UK200000000007"]);

        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000006", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchAnimal("UK200000000006", "Dead"));

        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000007", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CadsAnimal?)null);

        var service = CreateService();

        // Act
        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_TakesTheEarTagFromTheFileName_WhenThePatchedAnimalHasNone()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([], page: 1, totalRecords: 0));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UK200000000008"]);

        dataPatchStore.Setup(s => s.GetAnimalAsync(Cph, "UK200000000008", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchAnimal(null, "Alive"));

        var service = CreateService();

        // Act
        var animal = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).Single();

        // Assert
        Assert.Equal("UK200000000008", animal.EarTag);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsCadsAnimalsOnly_WhenTheDataPatchCannotBeRead()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalRecords: 1));

        dataPatchStore.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access Denied"));

        var service = CreateService();

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken))
            .Select(c => c.EarTag).ToList();

        // Assert
        Assert.Equal(["UK200000000001"], result);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_CallsAnimalDetailEndpointWithBasicAuth()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK200000000001", Parent("GeneticDam", "UK200000000098")));

        var service = CreateService();

        // Act
        await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(handler.Requests);

        Assert.Equal(HttpMethod.Get, item.Method);

        Assert.Equal(
            "http://fake-service.test/cads/api/v1/bovine/animals/UK200000000001",
            item.RequestUri!.ToString());

        Assert.Equal("Basic", item.Headers.Authorization!.Scheme);

        Assert.Equal(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{TestClient}:{TestPassphrase}")),
            item.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetCattleByCphAsync_SendsGatewayApiKeyAlongsideBasicAuthWhenConfigured()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var service = CreateService(apiOptions: WithGatewayKey());

        // Act
        await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);

        Assert.Equal(TestGatewayKey, Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task GetCattleByCphAsync_SendsNoGatewayApiKeyWhenNotConfigured()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var service = CreateService();

        // Act
        await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(handler.Requests).Headers.Contains("x-api-key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCattleByCphAsync_SendsNoGatewayApiKeyWhenGatewayApiKeyIsBlank(string gatewayApiKey)
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var service = CreateService(apiOptions: WithGatewayKey(gatewayApiKey));

        // Act
        await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(handler.Requests).Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_SendsGatewayApiKeyAlongsideBasicAuthWhenConfigured()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK200000000001"));

        var service = CreateService(apiOptions: WithGatewayKey());

        // Act
        await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);

        Assert.Equal(TestGatewayKey, Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_SendsNoGatewayApiKeyWhenNotConfigured()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK200000000001"));

        var service = CreateService();

        // Act
        await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(handler.Requests).Headers.Contains("x-api-key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAnimalDetailsAsync_SendsNoGatewayApiKeyWhenGatewayApiKeyIsBlank(string gatewayApiKey)
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK200000000001"));

        var service = CreateService(apiOptions: WithGatewayKey(gatewayApiKey));

        // Act
        await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(handler.Requests).Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_UnwrapsEnvelopeAndMapsAnimalDetail()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail(EarTag, Parent("GeneticDam", "UK200000000098")));

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EarTag, details.EarTag);
        Assert.Equal("Cattle", details.Species);
        Assert.Equal("Male", details.Sex);
        Assert.Equal(new DateOnly(2023, 2, 1), details.DateBirth);
        Assert.Equal(new DateOnly(2023, 2, 5), details.DateRegistered);
        Assert.Equal(new DateOnly(2023, 2, 15), details.DateOnCph);
        Assert.Equal("AA", details.BreedCode);
        Assert.Equal("Aberdeen Angus", details.BreedName);
        Assert.Equal("Aberdeen Angus", details.Breed);
        Assert.Equal("Alive", details.State);
        Assert.Equal("None", details.RestrictionStatus);
        Assert.Null(details.CurrentLocation);
        Assert.Null(details.RegisteredLocation);
        Assert.Null(details.DeathLocation);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_MapsLocationsToCattleDetailsResponse()
    {
        // Arrange
        const string json = """
                            {
                              "resourceType": "AnimalDetail",
                              "identifier": "UK200000000001",
                              "eventDateTime": "2026-09-24T10:00:00.000Z",
                              "source": { "system": "CTS", "schema": "animal_details", "schemaVersion": "1.0" },
                              "animalDetail": {
                                "resourceType": "Animal",
                                "identifier": { "schema": "ear-tag.conventional", "identifier": "UK200000000001" },
                                "species": "Cattle",
                                "sex": "Female",
                                "birthDate": "2023-02-01",
                                "registrationDate": "2023-02-05",
                                "dateOnCph": "2023-02-15",
                                "currentLocation": {
                                  "identifier": "22/001/0001",
                                  "subIdentifier": null,
                                  "fullIdentifier": "AH-22/001/0001",
                                  "name": "Current Holding Farm",
                                  "premisesType": "AH"
                                },
                                "registeredLocation": {
                                  "identifier": "22/002/0002",
                                  "subIdentifier": null,
                                  "fullIdentifier": "AH-22/002/0002",
                                  "name": "Birth Holding Farm",
                                  "premisesType": "AH"
                                },
                                "deathLocation": {
                                  "identifier": "22/003/0003",
                                  "subIdentifier": "SubA",
                                  "fullIdentifier": "AH-22/003/0003",
                                  "name": "Abattoir Location",
                                  "premisesType": "AB"
                                },
                                "breedCode": { "schema": "cts.breed", "breedName": "Aberdeen Angus", "identifier": "AA" },
                                "parentage": [],
                                "state": "Dead",
                                "restrictionStatus": "Restricted"
                              }
                            }
                            """;

        handler.RespondWith(HttpStatusCode.OK, json);

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(details.CurrentLocation);
        Assert.Equal("22/001/0001", details.CurrentLocation.Identifier);
        Assert.Null(details.CurrentLocation.SubIdentifier);
        Assert.Equal("AH-22/001/0001", details.CurrentLocation.FullIdentifier);
        Assert.Equal("Current Holding Farm", details.CurrentLocation.Name);
        Assert.Equal("AH", details.CurrentLocation.PremisesType);

        Assert.NotNull(details.RegisteredLocation);
        Assert.Equal("22/002/0002", details.RegisteredLocation.Identifier);
        Assert.Equal("AH-22/002/0002", details.RegisteredLocation.FullIdentifier);
        Assert.Equal("Birth Holding Farm", details.RegisteredLocation.Name);
        Assert.Equal("AH", details.RegisteredLocation.PremisesType);

        Assert.NotNull(details.DeathLocation);
        Assert.Equal("22/003/0003", details.DeathLocation.Identifier);
        Assert.Equal("SubA", details.DeathLocation.SubIdentifier);
        Assert.Equal("AH-22/003/0003", details.DeathLocation.FullIdentifier);
        Assert.Equal("Abattoir Location", details.DeathLocation.Name);
        Assert.Equal("AB", details.DeathLocation.PremisesType);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReportsAGeneticDamWhenThatIsTheOnlyDam()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Detail(EarTag, Parent("GeneticDam", "UK200000000098"), Parent("Sire", "UK200000000099")));

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("genetic", details.DamType);
        Assert.Equal("UK200000000098", details.GeneticDamEarTag);
        Assert.Null(details.SurrogateDamEarTag);
        Assert.Equal("UK200000000099", details.SireEarTag);
        Assert.Null(details.SireName);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReportsASurrogateDamEvenWhenAGeneticDamIsAlsoRecorded()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Detail(EarTag, Parent("GeneticDam", "UK200000000090"), Parent("SurrogateDam", "UK200000000091")));

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("surrogate", details.DamType);
        Assert.Equal("UK200000000090", details.GeneticDamEarTag);
        Assert.Equal("UK200000000091", details.SurrogateDamEarTag);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_LeavesDamAndSireUnsetWhenNoParentageIsRecorded()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail(EarTag));

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(details.DamType);
        Assert.Null(details.GeneticDamEarTag);
        Assert.Null(details.SurrogateDamEarTag);
        Assert.Null(details.SireEarTag);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_EscapesTheEarTagInTheResourceUrl()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK2 0000 00001"));

        var service = CreateService();

        // Act
        await service.GetAnimalDetailsAsync("UK2 0000 00001", TestContext.Current.CancellationToken);

        // Assert
        // AbsoluteUri, not ToString(), which renders %20 back as a space.
        Assert.Equal(
            "http://fake-service.test/cads/api/v1/bovine/animals/UK2%200000%2000001",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ThrowsNotFound_WhenCadsReturns404()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReadsTheAnimalFromTheDataPatch_WhenCadsReturns404()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        dataPatchStore.Setup(s => s.GetAnimalDetailsAsync(EarTag, It.IsAny<CancellationToken>())).ReturnsAsync(
            new CadsAnimalDetail(
                ResourceType: "Animal",
                Identifier: new CadsIdentifier("uk.gov.defra.ear-tag.conventional", EarTag),
                Species: "Cattle",
                Sex: "Female",
                BirthDate: new DateOnly(2022, 1, 1),
                RegistrationDate: new DateOnly(2022, 1, 5),
                DateOnCph: new DateOnly(2022, 2, 1),
                BreedCode: new CadsBreedCode("cts.breed", "Highland", "HL"),
                Parentage: [],
                State: "Alive",
                RestrictionStatus: "None"));

        var service = CreateService();

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EarTag, details.EarTag);
        Assert.Equal("Female", details.Sex);
        Assert.Equal(new DateOnly(2022, 1, 1), details.DateBirth);
        Assert.Equal("HL", details.BreedCode);
        Assert.Equal("Highland", details.BreedName);
        Assert.Equal("Alive", details.State);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ThrowsNotFoundWithoutReadingTheDataPatch_WhenThePatchIsSwitchedOff()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        var service = CreateService(new CadsDataPatchOptions { BucketName = " " });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));
        dataPatchStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCattleByCphAsync_DoesNotReadTheDataPatch_WhenThePatchIsSwitchedOff()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Page([Animal("UK200000000001", "Alive")], page: 1, totalRecords: 1));

        var service = CreateService(new CadsDataPatchOptions { BucketName = null });

        // Act
        var result = (await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken))
            .Select(c => c.EarTag).ToList();

        // Assert
        Assert.Equal(["UK200000000001"], result);
        dataPatchStore.Verify(
            s => s.GetCphEarTagsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ThrowsNotFound_WhenTheEnvelopeCarriesNoAnimal()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            """{"resourceType":"AnimalDetail","identifier":"UK200000000001","animalDetail":null}""");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_PropagatesOtherUpstreamFailures()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.Unauthorized, """{"title":"Unauthorized","status":401}""");

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Deserializes_ExpandedCadsAnimalDetailResponse_WithLocations()
    {
        // Arrange
        const string json = """
                            {
                                "resourceType": "AnimalDetail",
                                "identifier": "UK123456700001",
                                "eventDateTime": "2025-06-15T08:30:00Z",
                                "source": {
                                    "system": "CTS",
                                    "schema": "animal_details",
                                    "schemaVersion": "1.0"
                                },
                                "animalDetail": {
                                    "resourceType": "Animal",
                                    "identifier": {
                                        "schema": "ear-tag.conventional",
                                        "identifier": "UK123456700001"
                                    },
                                    "species": "Cattle",
                                    "sex": "Female",
                                    "birthDate": "2023-01-10",
                                    "registrationDate": "2023-01-15",
                                    "dateOnCPH": "2023-05-20",
                                    "currentLocation": {
                                        "identifier": "12/345/6789",
                                        "subIdentifier": null,
                                        "fullIdentifier": "AH-12/345/6789",
                                        "name": "TEST-FARM-LOCATION",
                                        "premisesType": "AH"
                                    },
                                    "registeredLocation": {
                                        "identifier": "11/222/3333",
                                        "subIdentifier": null,
                                        "fullIdentifier": "AH-11/222/3333",
                                        "name": "BIRTH-FARM-LOCATION",
                                        "premisesType": "AH"
                                    },
                                    "deathLocation": {
                                        "identifier": null,
                                        "subIdentifier": null,
                                        "fullIdentifier": null,
                                        "name": null,
                                        "premisesType": null
                                    },
                                    "breedCode": {
                                        "schema": "cts.breed",
                                        "breedName": "HOLSTEIN FRIESIAN",
                                        "identifier": "HF"
                                    },
                                    "parentage": [
                                        {
                                            "relationship": "GeneticDam",
                                            "animalIdentifier": {
                                                "schema": "ear-tag.conventional",
                                                "identifier": "UK123456700002"
                                            }
                                        },
                                        {
                                            "relationship": "Sire",
                                            "animalIdentifier": {
                                                "schema": "ear-tag.conventional",
                                                "identifier": "UK123456700003"
                                            }
                                        }
                                    ],
                                    "state": "Alive",
                                    "restrictionStatus": "Unrestricted"
                                }
                            }
                            """;

        // Act
        var response = System.Text.Json.JsonSerializer.Deserialize<CadsAnimalDetailResponse>(
            json,
            Services.Upstream.UpstreamJson.Options);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("AnimalDetail", response.ResourceType);
        Assert.Equal("UK123456700001", response.Identifier);
        Assert.NotNull(response.Source);
        Assert.Equal("CTS", response.Source.System);

        var detail = response.AnimalDetail;

        Assert.NotNull(detail);
        Assert.Equal("Animal", detail.ResourceType);
        Assert.Equal("UK123456700001", detail.Identifier?.Identifier);
        Assert.Equal("Cattle", detail.Species);
        Assert.Equal("Female", detail.Sex);
        Assert.Equal(new DateOnly(2023, 1, 10), detail.BirthDate);
        Assert.Equal(new DateOnly(2023, 1, 15), detail.RegistrationDate);
        Assert.Equal(new DateOnly(2023, 5, 20), detail.DateOnCph);

        Assert.NotNull(detail.CurrentLocation);
        Assert.Equal("12/345/6789", detail.CurrentLocation.Identifier);
        Assert.Null(detail.CurrentLocation.SubIdentifier);
        Assert.Equal("AH-12/345/6789", detail.CurrentLocation.FullIdentifier);
        Assert.Equal("TEST-FARM-LOCATION", detail.CurrentLocation.Name);
        Assert.Equal("AH", detail.CurrentLocation.PremisesType);

        Assert.NotNull(detail.RegisteredLocation);
        Assert.Equal("11/222/3333", detail.RegisteredLocation.Identifier);
        Assert.Equal("AH-11/222/3333", detail.RegisteredLocation.FullIdentifier);
        Assert.Equal("BIRTH-FARM-LOCATION", detail.RegisteredLocation.Name);
        Assert.Equal("AH", detail.RegisteredLocation.PremisesType);

        Assert.NotNull(detail.DeathLocation);
        Assert.Null(detail.DeathLocation.Identifier);

        Assert.NotNull(detail.BreedCode);
        Assert.Equal("HF", detail.BreedCode.Identifier);
        Assert.Equal("HOLSTEIN FRIESIAN", detail.BreedCode.BreedName);

        Assert.NotNull(detail.Parentage);
        Assert.Equal(2, detail.Parentage.Count);
        Assert.Equal("Alive", detail.State);
        Assert.Equal("Unrestricted", detail.RestrictionStatus);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_RejectsBlankEarTag()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAnimalDetailsAsync(" ", TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void CadsApiOptions_DefaultsToSixtySecondTimeouts()
    {
        // Act
        var options = new CadsApiOptions();

        // Assert
        Assert.Equal(60_000, options.GetAnimalsOnHoldingTimeout);
        Assert.Equal(60_000, options.GetAnimalDetailsTimeout);
    }

    [Fact]
    public async Task GetCattleByCphAsync_UsesConfiguredTimeout()
    {
        // Arrange
        handler.RespondWith(
            HttpStatusCode.OK,
            Page([Animal("UK200000000001", "Passport Produced")], page: 1, totalRecords: 1));

        var options = new CadsApiOptions
        {
            BaseUrl = Options.BaseUrl,
            ClientId = Options.ClientId,
            ClientSecret = Options.ClientSecret,
            PageSize = Options.PageSize,
            GetAnimalsOnHoldingTimeout = 45_000,
        };

        var service = CreateService(apiOptions: options);

        // Act
        var result = await service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_UsesConfiguredTimeout()
    {
        // Arrange
        handler.RespondWith(HttpStatusCode.OK, Detail("UK200000000001"));

        var options = new CadsApiOptions
        {
            BaseUrl = Options.BaseUrl,
            ClientId = Options.ClientId,
            ClientSecret = Options.ClientSecret,
            GetAnimalDetailsTimeout = 45_000,
        };

        var service = CreateService(apiOptions: options);

        // Act
        var details = await service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EarTag, details.EarTag);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ThrowsCircuitBreakerOpenException_WhenCircuitBreakerIsOpen()
    {
        // Arrange
        var cbFactoryMock = new Mock<ICircuitBreakerStrategyFactory<CadsService>>();
        var cbStrategyMock = new Mock<ICircuitBreakerStrategy<CadsService>>();

        cbFactoryMock.Setup(f => f.BuildCircuitBreakerStrategy()).Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithCircuitKey("get-animal-details-from-cads")).Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithCancellationToken(It.IsAny<CancellationToken>()))
            .Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithAction(It.IsAny<Func<CancellationToken, Task<CadsAnimalDetailResponse>>>()))
            .Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.Execute<CadsAnimalDetailResponse>())
            .ThrowsAsync(new CircuitBreakerOpenException("Circuit is open"));

        var service = CreateService(cbStrategyFactory: cbFactoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));

        dataPatchStore.Verify(
            s => s.GetAnimalDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ThrowsCircuitBreakerOpenException_WhenCircuitBreakerIsOpen()
    {
        // Arrange
        var cbFactoryMock = new Mock<ICircuitBreakerStrategyFactory<CadsService>>();
        var cbStrategyMock = new Mock<ICircuitBreakerStrategy<CadsService>>();

        cbFactoryMock.Setup(f => f.BuildCircuitBreakerStrategy()).Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithCircuitKey("get-animals-on-holding-paged-from-cads"))
            .Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithCancellationToken(It.IsAny<CancellationToken>()))
            .Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.WithAction(It.IsAny<Func<CancellationToken, Task<CadsAnimalsOnHolding>>>()))
            .Returns(cbStrategyMock.Object);
        cbStrategyMock.Setup(s => s.Execute<CadsAnimalsOnHolding>())
            .ThrowsAsync(new CircuitBreakerOpenException("Circuit is open"));

        var service = CreateService(cbStrategyFactory: cbFactoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task
        GetAnimalDetailsAsync_ThrowsCircuitBreakerOpenException_WhenConsecutiveFailuresTripCircuitBreaker()
    {
        // Arrange
        handler
            .RespondWith(HttpStatusCode.InternalServerError, "CADS error")
            .RespondWith(HttpStatusCode.InternalServerError, "CADS error");

        var options = new CadsApiOptions
        {
            BaseUrl = Options.BaseUrl,
            ClientId = Options.ClientId,
            ClientSecret = Options.ClientSecret,
            CircuitBreakerConsecutiveFailureThreshold = 2,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDuration = 30_000,
            CircuitBreakerDurationOfBreak = 30_000,
        };

        var service = CreateService(apiOptions: options);

        // Act & Assert
        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            service.GetAnimalDetailsAsync(EarTag, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleByCphAsync_ThrowsCircuitBreakerOpenException_WhenConsecutiveFailuresTripCircuitBreaker()
    {
        // Arrange
        handler
            .RespondWith(HttpStatusCode.InternalServerError, "CADS error")
            .RespondWith(HttpStatusCode.InternalServerError, "CADS error");

        var options = new CadsApiOptions
        {
            BaseUrl = Options.BaseUrl,
            ClientId = Options.ClientId,
            ClientSecret = Options.ClientSecret,
            CircuitBreakerConsecutiveFailureThreshold = 2,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDuration = 30_000,
            CircuitBreakerDurationOfBreak = 30_000,
        };

        var service = CreateService(apiOptions: options);

        // Act & Assert
        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<RestResponseException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            service.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        handler.Dispose();
    }

    private static string Parent(string relationship, string earTag) => $$"""
                                                                          {
                                                                            "relationship": "{{relationship}}",
                                                                            "animalIdentifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "{{earTag}}" }
                                                                          }
                                                                          """;

    private static string Detail(string earTag, params string[] parentage) => $$"""
                                                                                {
                                                                                  "resourceType": "AnimalDetail",
                                                                                  "identifier": "{{earTag}}",
                                                                                  "eventDateTime": "2026-09-24T10:00:00.000Z",
                                                                                  "source": { "system": "CTS", "schema": "animal_details", "schemaVersion": "1.0" },
                                                                                  "animalDetail": {
                                                                                    "resourceType": "Animal",
                                                                                    "identifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "{{earTag}}" },
                                                                                    "species": "Cattle",
                                                                                    "sex": "Male",
                                                                                    "birthDate": "2023-02-01",
                                                                                    "registrationDate": "2023-02-05",
                                                                                    "dateOnCph": "2023-02-15",
                                                                                    "breedCode": { "schema": "cts.breed", "breedName": "Aberdeen Angus", "identifier": "AA" },
                                                                                    "parentage": [{{string.Join(",", parentage)}}],
                                                                                    "state": "Alive",
                                                                                    "restrictionStatus": "None"
                                                                                  }
                                                                                }
                                                                                """;

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

    private static string Page(string[] animals, int page, long totalRecords) => $$"""
          {
            "resourceType": "AnimalCollection",
            "CPH": { "schema": "uk.gov.defra.cph", "identifier": "22/001/0001" },
            "locationName": "Oakfield Farm",
            "animals": [{{string.Join(",", animals)}}],
            "totalRecords": {{totalRecords}},
            "page": {{page}},
            "pageSize": 2
          }
          """;

    private static CadsAnimal PatchAnimal(string? earTag, string status) => new(
        earTag is null ? null : new CadsIdentifier("uk.gov.defra.ear-tag.conventional", earTag),
        new DateOnly(2023, 2, 1),
        new DateOnly(2023, 2, 15),
        null,
        "Cattle",
        "Female",
        new CadsBreedCode("cts.breed", "Aberdeen Angus", "AA"),
        status);

    private static CadsApiOptions WithGatewayKey(string gatewayApiKey = TestGatewayKey) => new()
    {
        BaseUrl = Options.BaseUrl,
        ClientId = Options.ClientId,
        ClientSecret = Options.ClientSecret,
        PageSize = Options.PageSize,
        GatewayApiKey = gatewayApiKey,
    };

    private CadsService CreateService(
        CadsDataPatchOptions? dataPatchOptions = null,
        CadsApiOptions? apiOptions = null,
        ICircuitBreakerStrategyFactory<CadsService>? cbStrategyFactory = null)
    {
        var options = apiOptions ?? Options;
        if (options.CircuitBreakerConsecutiveFailureThreshold is null && options.CircuitBreakerFailureRatio is null)
        {
            options = new CadsApiOptions
            {
                BaseUrl = options.BaseUrl,
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                PageSize = options.PageSize,
                GatewayApiKey = options.GatewayApiKey,
                GetAnimalsOnHoldingTimeout = options.GetAnimalsOnHoldingTimeout,
                GetAnimalDetailsTimeout = options.GetAnimalDetailsTimeout,
                CircuitBreakerConsecutiveFailureThreshold = 5,
                CircuitBreakerSamplingDuration = options.CircuitBreakerSamplingDuration,
                CircuitBreakerDurationOfBreak = options.CircuitBreakerDurationOfBreak,
                CircuitBreakerMinimumThroughput = options.CircuitBreakerMinimumThroughput,
            };
        }

        var provider = RestStrategyTestFactory.CreateServiceProvider(
            handler,
            services => services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options)));

        return new CadsService(
            provider.GetRequiredService<IRestStrategyFactory<CadsService>>(),
            cbStrategyFactory ?? provider.GetRequiredService<ICircuitBreakerStrategyFactory<CadsService>>(),
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(dataPatchOptions ?? DataPatchOptions),
            dataPatchStore.Object,
            NullLogger<CadsService>.Instance);
    }
}
