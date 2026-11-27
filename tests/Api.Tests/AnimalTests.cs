// <copyright file="AnimalTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Globalization;
using Defra.Lis.Entities;

public class AnimalTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-26T10:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public void Constructor_CreatesAPendingRecordWithTheDetailsReceivedAndLinksItToItsSubmission()
    {
        var submission = new Submission("REF-1003", "10/081/1234", "test user");
        var submissionAnimal = submission.AddAnimal(
            earTag: "UK 12 3456 100001",
            dateBirth: new DateOnly(2026, 2, 1),
            sex: "female",
            breed: "AA",
            damType: "surrogate",
            damGeneticEarTag: "UK 12 3456 000002",
            damSurrogateEarTag: "UK 12 3456 000003",
            sireEarTag: "UK 12 3456 000010",
            sireName: "Example sire");

        var animal = new Animal(submissionAnimal, "UK123456100001");

        Assert.NotEqual(Guid.Empty, animal.Id);
        Assert.Equal("UK123456100001", animal.EarTag);
        Assert.Equal(AnimalStatuses.Pending, animal.Status);
        Assert.Equal(submission.Id, animal.SubmissionId);
        Assert.Equal(submissionAnimal.Id, animal.SubmissionAnimalId);
        Assert.Equal(new DateOnly(2026, 2, 1), animal.DateBirth);
        Assert.Equal("female", animal.Sex);
        Assert.Equal("AA", animal.Breed);
        Assert.Equal("surrogate", animal.DamType);
        Assert.Equal("UK 12 3456 000002", animal.DamGeneticEarTag);
        Assert.Equal("UK 12 3456 000003", animal.DamSurrogateEarTag);
        Assert.Equal("UK 12 3456 000010", animal.SireEarTag);
        Assert.Equal("Example sire", animal.SireName);
        Assert.Null(animal.UpdatedAt);
        Assert.Empty(animal.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RequiresAnEarTag(string? earTag)
    {
        var submissionAnimal = new Submission("REF-1003", "10/081/1234", "test user").AddAnimal(string.Empty);

        Assert.ThrowsAny<ArgumentException>(() => new Animal(submissionAnimal, earTag!));
    }

    [Fact]
    public void RecordCtsAccepted_RegistersAPendingAnimalOnce()
    {
        var animal = NewAnimal();

        Assert.True(animal.RecordCtsAccepted(At));
        Assert.Equal(AnimalStatuses.Registered, animal.Status);
        Assert.Equal(At, animal.UpdatedAt);

        Assert.False(animal.RecordCtsAccepted(At.AddMinutes(1)));
        Assert.Equal(At, animal.UpdatedAt);
    }

    [Fact]
    public void RecordCtsRejected_MovesAPendingAnimalToErrorWithOneErrorPerCode()
    {
        var animal = NewAnimal();

        Assert.True(animal.RecordCtsRejected([("CTWS014", "Invalid Breed Code"), ("CTWS003", "Missing Ear Tag"), ("CTWS014", "Invalid Breed Code")], At));

        Assert.Equal(AnimalStatuses.Error, animal.Status);
        Assert.Equal(At, animal.UpdatedAt);
        Assert.Equal(["CTWS003", "CTWS014"], animal.Errors.Select(e => e.ErrorCode).Order(StringComparer.Ordinal));
        Assert.All(animal.Errors, e =>
        {
            Assert.Equal(animal.Id, e.AnimalId);
            Assert.Equal(Animal.CtsErrorSource, e.Source);
            Assert.Equal(At, e.CreatedAt);
        });
    }

    [Fact]
    public void RecordCtsRejected_WhenTheSameErrorsAreRecordedAgain_AddsNothing()
    {
        var animal = NewAnimal();
        animal.RecordCtsRejected([("CTWS014", "Invalid Breed Code")], At);

        Assert.False(animal.RecordCtsRejected([("CTWS014", "Invalid Breed Code")], At.AddMinutes(1)));

        Assert.Single(animal.Errors);
        Assert.Equal(At, animal.UpdatedAt);
    }

    [Fact]
    public void RecordCtsRejected_SkipsBlankCodesAndUsesTheCodeWhenTheTextIsBlank()
    {
        var animal = NewAnimal();

        animal.RecordCtsRejected([(string.Empty, "No code"), ("CTWS204", " ")], At);

        var error = Assert.Single(animal.Errors);
        Assert.Equal("CTWS204", error.ErrorCode);
        Assert.Equal("CTWS204", error.ErrorText);
    }

    [Fact]
    public void RecordCtsRejected_DoesNotChangeTheStatusOfARegisteredAnimal()
    {
        var animal = NewAnimal();
        animal.RecordCtsAccepted(At);

        animal.RecordCtsRejected([("CTWS014", "Invalid Breed Code")], At.AddMinutes(1));

        Assert.Equal(AnimalStatuses.Registered, animal.Status);
    }

    private static Animal NewAnimal()
    {
        var submissionAnimal = new Submission("REF-1003", "10/081/1234", "test user").AddAnimal("UK123456100001");
        return new Animal(submissionAnimal, "UK123456100001");
    }
}
