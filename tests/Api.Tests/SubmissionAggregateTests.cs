using Lis.Cattle;

namespace Lis.Cattle.Tests;

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
        Assert.Equal("submitted", submission.Status);
        Assert.True(submission.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Empty(submission.Animals);
    }

    [Theory]
    [InlineData("", "12/345/6789", "tester", "submitted")]
    [InlineData("CLIENT-123", "", "tester", "submitted")]
    [InlineData("CLIENT-123", "12/345/6789", "", "submitted")]
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
            status: "submitted",
            dateBirth: new DateOnly(2025, 1, 1),
            sex: "M",
            breed: "Angus");

        Assert.Single(submission.Animals);
        Assert.Equal(submission.Id, animal.SubmissionId);
        Assert.Equal("UK123456700001", animal.EarTag);
        Assert.Equal("submitted", animal.Status);
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
        submission.UpdateStatus("processing");
        Assert.Equal("processing", submission.Status);

        var animal = submission.AddAnimal("UK123456700001");
        animal.UpdateStatus("complete");
        Assert.Equal("complete", animal.Status);
    }
}