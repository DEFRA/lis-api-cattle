// <copyright file="SubmissionAggregateTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Entities;

public class SubmissionAggregateTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesSubmission()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");

        Assert.NotEqual(Guid.Empty, submission.Id);
        Assert.Equal("CLIENT-123", submission.ClientReference);
        Assert.Equal("12/345/6789", submission.CountyParishHolding);
        Assert.Equal("tester", submission.SubmittedBy);
        Assert.Equal(Statuses.Submitted, submission.Status);
        Assert.True(submission.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Empty(submission.Animals);
    }

    [Theory]
    [InlineData("", "12/345/6789", "tester", Statuses.Submitted)]
    [InlineData("CLIENT-123", "", "tester", Statuses.Submitted)]
    [InlineData("CLIENT-123", "12/345/6789", "", Statuses.Submitted)]
    [InlineData("CLIENT-123", "12/345/6789", "tester", "")]
    public void Constructor_WithInvalidArguments_ThrowsArgumentException(string clientRef, string cph, string submittedBy, string status)
    {
        Assert.Throws<ArgumentException>(() => new Submission(clientRef, cph, submittedBy, status));
    }

    [Fact]
    public void AddAnimal_AddsAnimalToSubmissionAndEnforcesAggregateConsistency()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");
        var animal = submission.AddAnimal(
            earTag: "UK123456700001",
            status: Statuses.Submitted,
            dateBirth: new DateOnly(2025, 1, 1),
            sex: "M",
            breed: "Angus");

        Assert.Single(submission.Animals);
        Assert.Equal(submission.Id, animal.SubmissionId);
        Assert.Equal("UK123456700001", animal.EarTag);
        Assert.Equal(Statuses.Submitted, animal.Status);
        Assert.Equal(new DateOnly(2025, 1, 1), animal.DateBirth);
        Assert.Equal("M", animal.Sex);
        Assert.Equal("Angus", animal.Breed);
    }

    [Fact]
    public void AddError_AddsErrorToAnimal()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");
        var animal = submission.AddAnimal("UK123456700001");
        var error = animal.AddError("ERR001", "Invalid ear tag check digit");

        Assert.Single(animal.Errors);
        Assert.Equal(animal.Id, error.AnimalId);
        Assert.Equal("ERR001", error.ErrorCode);
        Assert.Equal("Invalid ear tag check digit", error.ErrorText);
    }

    [Fact]
    public void UpdateStatus_UpdatesStatusCorrectly()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");
        submission.UpdateStatus(Statuses.Processing);
        Assert.Equal(Statuses.Processing, submission.Status);

        var animal = submission.AddAnimal("UK123456700001");
        animal.UpdateStatus(Statuses.Complete);
        Assert.Equal(Statuses.Complete, animal.Status);
    }

    [Fact]
    public void MarkMethods_UpdateStatusCorrectly()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");
        submission.MarkAsProcessing();
        Assert.Equal(Statuses.Processing, submission.Status);

        submission.MarkAsError();
        Assert.Equal(Statuses.Error, submission.Status);

        submission.MarkAsComplete();
        Assert.Equal(Statuses.Complete, submission.Status);

        var animal = submission.AddAnimal("UK123456700001");
        animal.MarkAsProcessing();
        Assert.Equal(Statuses.Processing, animal.Status);

        animal.MarkAsError("ERR01", "Some error");
        Assert.Equal(Statuses.Error, animal.Status);
        Assert.Single(animal.Errors);

        animal.MarkAsComplete();
        Assert.Equal(Statuses.Complete, animal.Status);
        Assert.Empty(animal.Errors);
    }

    [Fact]
    public void RefreshStatusFromAnimals_ComputesCorrectStatus()
    {
        var submission = new Submission("CLIENT-123", "12/345/6789", "tester");
        var animal1 = submission.AddAnimal("UK001", status: Statuses.Complete);
        var animal2 = submission.AddAnimal("UK002", status: Statuses.Complete);

        submission.RefreshStatusFromAnimals();
        Assert.Equal(Statuses.Complete, submission.Status);

        animal2.MarkAsProcessing();
        submission.RefreshStatusFromAnimals();
        Assert.Equal(Statuses.Processing, submission.Status);

        animal1.MarkAsError("ERR", Statuses.Error);
        submission.RefreshStatusFromAnimals();
        Assert.Equal(Statuses.Error, submission.Status);
    }
}
