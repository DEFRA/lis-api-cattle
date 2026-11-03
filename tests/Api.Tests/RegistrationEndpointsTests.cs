// <copyright file="RegistrationEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Asp.Versioning;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class RegistrationEndpointsTests
{
    [Fact]
    public void MapRegistrationEndpoints_MapsGroupAndRouteSuccessfully()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();

        var returned = app.MapRegistrationEndpoints();

        Assert.NotNull(returned);
    }

    [Fact]
    public async Task CreateRegistrationBundle_CallsServiceAndReturnsCreatedResult()
    {
        var mockService = new Mock<ICattleService>();
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-MNBX4Q2A",
            Holding = new HoldingRequest
            {
                Cph = "10/081/1234",
            },
            SubmittedBy = "test user 1",
            Animals =
            [
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 12 3456 100003",
                    DateOfBirth = new DateOnly(2026, 2, 1),
                    Sex = "female",
                    Breed = "Aberdeen Angus",
                    Dam = new DamRegistrationRequest
                    {
                        Type = "surrogate",
                        GeneticDamEarTag = "UK 12 3456 000002",
                        SurrogateDamEarTag = "UK 12 3456 000003",
                    },
                    Sire = new SireRegistrationRequest
                    {
                        EarTag = "UK 12 3456 000010",
                        Name = "Example sire",
                    },
                },
            ],
        };

        var expected = new BundleResponse
        {
            Id = Guid.NewGuid(),
            ClientReference = request.ClientReference,
            CountyParishHolding = request.Holding.Cph,
            SubmittedBy = "BE4FE",
            Status = Statuses.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Animals =
            [
                new BundleAnimalResponse
                {
                    Id = Guid.NewGuid(),
                    EarTag = "UK 12 3456 100003",
                    Status = Statuses.Pending,
                    DateBirth = new DateOnly(2026, 2, 1),
                    Sex = "female",
                    Breed = "Aberdeen Angus",
                    DamType = "surrogate",
                    DamGeneticEarTag = "UK 12 3456 000002",
                    DamSurrogateEarTag = "UK 12 3456 000003",
                    SireEarTag = "UK 12 3456 000010",
                    SireName = "Example sire",
                },
            ],
        };

        mockService.Setup(s => s.CreateRegistrationBundleAsync(It.IsAny<RegistrationBundleRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().PostAsJsonAsync(
            "/v1/registrations/",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/v1/holdings/{expected.CountyParishHolding}/bundles", response.Headers.Location?.OriginalString);
        var result = await response.Content.ReadFromJsonAsync<BundleResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("REG-MNBX4Q2A", result.ClientReference);
        Assert.Equal(Statuses.Pending, result.Status);
        Assert.Single(result.Animals);
        Assert.Equal(Statuses.Pending, result.Animals[0].Status);
        Assert.Equal("UK 12 3456 100003", result.Animals[0].EarTag);
    }

    [Fact]
    public async Task ValidateRegistrationBundle_CallsValidationServiceAndReturnsResult()
    {
        var mockValidationService = new Mock<Validation.ISubmissionValidationService>();
        var submissionId = Guid.NewGuid();
        var expectedResult = new Validation.SubmissionValidationResult
        {
            SubmissionId = submissionId,
            IsValid = true,
            Status = Statuses.Complete,
        };

        mockValidationService.Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockValidationService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().PostAsync(
            $"/v1/registrations/{submissionId}/validate",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Validation.SubmissionValidationResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(Statuses.Complete, result.Status);
        mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRegistrationBundle_WhenRequestIsInvalid_ReturnsProblemDetails400()
    {
        var mockService = new Mock<ICattleService>();
        mockService
            .Setup(s => s.CreateRegistrationBundleAsync(It.IsAny<RegistrationBundleRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Client reference is required."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().PostAsJsonAsync(
            "/v1/registrations/",
            new RegistrationBundleRequest
            {
                ClientReference = string.Empty,
                Holding = new HoldingRequest { Cph = "10/081/1234" },
                SubmittedBy = "test user 1",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Invalid registration bundle request", problem.Title);
        Assert.Equal("Client reference is required.", problem.Detail);
    }

    [Theory]
    [InlineData("", "clientReference")]
    [InlineData("", "holding")]
    [InlineData("", "submittedBy")]
    [InlineData("holding", "cph")]
    [InlineData("animal", "earTag")]
    [InlineData("animal", "dateOfBirth")]
    [InlineData("animal", "sex")]
    [InlineData("animal", "breed")]
    [InlineData("animal", "dam")]
    [InlineData("dam", "type")]
    [InlineData("dam", "geneticDamEarTag")]
    [InlineData("dam", "surrogateDamEarTag")]
    public async Task CreateRegistrationBundle_WhenRequiredPropertyIsMissing_ReturnsBadRequest(
        string section,
        string property)
    {
        var payload = System.Text.Json.Nodes.JsonNode.Parse(
            """
            {
              "clientReference": "REG-123",
              "holding": { "cph": "10/081/1234" },
              "submittedBy": "test user 1",
              "animals": [{
                "earTag": "UK 12 3456 100003",
                "dateOfBirth": "2026-02-01",
                "sex": "female",
                "breed": "Aberdeen Angus",
                "dam": {
                  "type": "surrogate",
                  "geneticDamEarTag": "UK 12 3456 000002",
                  "surrogateDamEarTag": "UK 12 3456 000003"
                }
              }]
            }
            """);
        Assert.NotNull(payload);
        var animal = payload["animals"]?[0];
        var target = section switch
        {
            "holding" => payload["holding"],
            "animal" => animal,
            "dam" => animal?["dam"],
            _ => payload,
        };
        Assert.NotNull(target);
        Assert.True(target.AsObject().Remove(property));

        var mockService = new Mock<ICattleService>();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        await using var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().PostAsJsonAsync(
            "/v1/registrations/",
            payload,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        mockService.Verify(
            s => s.CreateRegistrationBundleAsync(It.IsAny<RegistrationBundleRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateRegistrationBundle_WhenSubmissionIsUnknown_ReturnsProblemDetails404()
    {
        var submissionId = Guid.NewGuid();
        var mockValidationService = new Mock<Validation.ISubmissionValidationService>();
        mockValidationService
            .Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Submission {submissionId} was not found."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockValidationService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().PostAsync(
            $"/v1/registrations/{submissionId}/validate",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Submission not found", problem.Title);
        Assert.Contains(submissionId.ToString(), problem.Detail);
    }
}
