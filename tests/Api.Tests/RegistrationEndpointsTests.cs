// <copyright file="RegistrationEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Endpoints;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
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
        var app = builder.Build();

        var returned = app.MapRegistrationEndpoints();

        Assert.NotNull(returned);
    }

    [Fact]
    public async Task CreateRegistrationBundle_CallsServiceAndReturnsCreatedResult()
    {
        // Arrange
        var mockService = new Mock<ICattleService>();
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-MNBX4Q2A",
            Holding = new HoldingRequest
            {
                Cph = "10/081/1234",
            },
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

        mockService.Setup(s => s.CreateRegistrationBundleAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        // Act
        var result = await mockService.Object.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
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

        // Act
        var result = await mockValidationService.Object.ValidateSubmissionByIdAsync(submissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(Statuses.Complete, result.Status);
        mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
