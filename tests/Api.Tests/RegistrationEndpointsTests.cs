// <copyright file="RegistrationEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Asp.Versioning;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Api.Validation;
using Defra.Lis.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    public async Task CreateRegistrationBundle_QueuesTheBundleAndReturnsItsIdWithoutStoringIt()
    {
        var publisher = new Mock<IRegistrationBundlePublisher>();
        RegistrationBundleMessage? published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RegistrationBundleMessage, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);
        await using var app = await StartAppAsync(publisher.Object);

        var response = await PostWithCorrelationIdAsync(app, ValidRequest(), "corr-925");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegistrationAcceptedResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("REG-MNBX4Q2A", result.ClientReference);
        Assert.Equal(RegistrationAcceptedResponse.AcceptedStatus, result.Status);

        Assert.NotNull(published);
        Assert.Equal(result.Id, published.SubmissionId);
        Assert.Equal("corr-925", published.CorrelationId);
        Assert.Equal("REG-MNBX4Q2A", published.Bundle.ClientReference);
        Assert.Single(published.Bundle.Animals);
    }

    [Fact]
    public async Task CreateRegistrationBundle_GivesEveryBundleItsOwnId()
    {
        var publisher = new Mock<IRegistrationBundlePublisher>();
        await using var app = await StartAppAsync(publisher.Object);

        var first = await (await PostWithCorrelationIdAsync(app, ValidRequest(), "corr-1")).Content
            .ReadFromJsonAsync<RegistrationAcceptedResponse>(TestContext.Current.CancellationToken);
        var second = await (await PostWithCorrelationIdAsync(app, ValidRequest(), "corr-2")).Content
            .ReadFromJsonAsync<RegistrationAcceptedResponse>(TestContext.Current.CancellationToken);

        Assert.NotEqual(first!.Id, second!.Id);
    }

    [Fact]
    public async Task CreateRegistrationBundle_WhenSqsIsUnavailable_Returns503NotA500()
    {
        var publisher = new Mock<IRegistrationBundlePublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("The registration bundle could not be queued.", new HttpRequestException("connection refused")));
        await using var app = await StartAppAsync(publisher.Object);

        var response = await PostWithCorrelationIdAsync(app, ValidRequest(), "corr-503");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
    }

    [Theory]
    [InlineData("clientReference", "")]
    [InlineData("cph", "101081234")]
    [InlineData("earTag", "")]
    [InlineData("earTag", "UK 12 3456 1000034")]
    [InlineData("sex", "unknown")]
    [InlineData("breed", "Aberdeen Angus")]
    [InlineData("breed", "aa")]
    [InlineData("breed", "ABCDEF")]
    [InlineData("geneticDamEarTag", "")]
    [InlineData("surrogateDamEarTag", "UK 12 3456 0000031")]
    [InlineData("geneticDamEarTag", "UK A1213 000031")]
    [InlineData("type", "")]
    public async Task CreateRegistrationBundle_WhenShapeIsInvalid_Returns400ProblemDetailsAndQueuesNothing(string field, string value)
    {
        var request = ValidRequest();
        var animal = request.Animals[0];
        switch (field)
        {
            case "clientReference": request.ClientReference = value; break;
            case "cph": request.Holding.Cph = value; break;
            case "earTag": animal.EarTag = value; break;
            case "sex": animal.Sex = value; break;
            case "breed": animal.Breed = value; break;
            case "geneticDamEarTag": animal.Dam.GeneticDamEarTag = value; break;
            case "surrogateDamEarTag": animal.Dam.SurrogateDamEarTag = value; break;
            case "type": animal.Dam.Type = value; break;
        }

        var publisher = new Mock<IRegistrationBundlePublisher>();
        await using var app = await StartAppAsync(publisher.Object);

        var response = await PostWithCorrelationIdAsync(app, request, "corr-400");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Http.HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotEmpty(problem.Errors);
        publisher.Verify(p => p.PublishAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateRegistrationBundle_WhenThereAreNoAnimals_Returns400AndQueuesNothing()
    {
        var request = ValidRequest();
        request.Animals.Clear();
        var publisher = new Mock<IRegistrationBundlePublisher>();
        await using var app = await StartAppAsync(publisher.Object);

        var response = await PostWithCorrelationIdAsync(app, request, "corr-400");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        publisher.Verify(p => p.PublishAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("M")]
    [InlineData("male")]
    [InlineData("F")]
    [InlineData("Female")]
    public async Task CreateRegistrationBundle_AcceptsTheSexValuesTheJourneyUses(string sex)
    {
        var request = ValidRequest();
        request.Animals[0].Sex = sex;
        request.Animals[0].Dam.SurrogateDamEarTag = string.Empty;
        request.Animals[0].Sire = null;
        var publisher = new Mock<IRegistrationBundlePublisher>();
        await using var app = await StartAppAsync(publisher.Object);

        var response = await PostWithCorrelationIdAsync(app, request, "corr-sex");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
                "breed": "AA",
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

        var publisher = new Mock<IRegistrationBundlePublisher>();
        await using var app = await StartAppAsync(publisher.Object);

        var response = await app.GetAuthenticatedTestClient().PostAsJsonAsync(
            "/v1/registrations/",
            payload,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()),
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

    private static RegistrationBundleRequest ValidRequest() => new()
    {
        ClientReference = "REG-MNBX4Q2A",
        Holding = new HoldingRequest { Cph = "10/081/1234" },
        SubmittedBy = "test user 1",
        Animals =
        [
            new AnimalRegistrationRequest
            {
                EarTag = "UK 12 3456 100003",
                DateOfBirth = new DateOnly(2026, 2, 1),
                Sex = "female",
                Breed = "AA",
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

    private static async Task<WebApplication> StartAppAsync(IRegistrationBundlePublisher publisher)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddCorrelationId();
        builder.Services.AddScoped<FluentValidation.IValidator<RegistrationBundleRequest>, RegistrationBundleRequestValidator>();
        builder.Services.AddSingleton(publisher);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseCorrelationId();
        app.UseTestServiceToServiceAuthentication();
        app.MapRegistrationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<HttpResponseMessage> PostWithCorrelationIdAsync(WebApplication app, RegistrationBundleRequest request, string correlationId)
    {
        var client = app.GetAuthenticatedTestClient();
        client.DefaultRequestHeaders.Add(TraceHeaders.CdpRequestId, correlationId);
        return await client.PostAsJsonAsync("/v1/registrations/", request, TestContext.Current.CancellationToken);
    }
}
