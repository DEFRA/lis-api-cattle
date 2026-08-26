// <copyright file="FakeCtsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Services;
using Defra.Lis.Entities;

public class FakeCtsServiceTests
{
    private readonly FakeCtsService fakeCtsService = new();

    [Fact]
    public async Task SubmitAnimalRegistrationAsync_WithNormalEarTag_ReturnsProcessing()
    {
        var submission = new Submission("CLIENT1", "10/100/1000", "testUser");
        var animal = submission.AddAnimal("UK123456700001");

        var response = await fakeCtsService.SubmitAnimalRegistrationAsync(animal, TestContext.Current.CancellationToken);

        Assert.Equal("UK123456700001", response.EarTag);
        Assert.Equal(Statuses.Processing, response.Status);
        Assert.False(response.IsError);
        Assert.False(response.IsClean);
    }

    [Fact]
    public async Task SubmitAnimalRegistrationAsync_WithSubmitErrTag_ReturnsError()
    {
        var submission = new Submission("CLIENT1", "10/100/1000", "testUser");
        var animal = submission.AddAnimal("UK_SUBMIT_ERR_01");

        var response = await fakeCtsService.SubmitAnimalRegistrationAsync(animal, TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.Single(response.Errors);
        Assert.Equal("CTS_SUBMIT_FAIL", response.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task CheckAnimalStatusAsync_WithCleanTag_ReturnsClean()
    {
        var response = await fakeCtsService.CheckAnimalStatusAsync("UK123456700001", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(response.IsClean);
        Assert.False(response.IsError);
        Assert.Equal("clean", response.Status);
    }

    [Fact]
    public async Task CheckAnimalStatusAsync_WithErrorTag_ReturnsError()
    {
        var response = await fakeCtsService.CheckAnimalStatusAsync("UK_ERR_999", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.False(response.IsClean);
        Assert.Single(response.Errors);
        Assert.Equal("CTS_VALIDATION_ERROR", response.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task CheckAnimalStatusAsync_WithPendingTag_ReturnsProcessing()
    {
        var response = await fakeCtsService.CheckAnimalStatusAsync("UK_PENDING_01", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(response.IsClean);
        Assert.False(response.IsError);
        Assert.Equal(Statuses.Processing, response.Status);
    }
}
