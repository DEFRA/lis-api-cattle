// <copyright file="SubmissionCtsResultsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Entities;

public class SubmissionCtsResultsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void TryCompleteFromCtsResults_WhenEveryAnimalWasAccepted_CompletesTheBundle()
    {
        var (submission, first, second) = Bundle();
        first.RecordCtsAccepted();
        second.RecordCtsAccepted();

        Assert.True(submission.TryCompleteFromCtsResults(Now));

        Assert.Equal(Statuses.Complete, submission.Status);
        Assert.Equal(Now, submission.CompletedAt);
        Assert.Equal(Now, submission.UpdatedAt);
    }

    [Fact]
    public void TryCompleteFromCtsResults_WhenAnAnimalWasRejected_CompletesWithErrors()
    {
        var (submission, first, second) = Bundle();
        first.RecordCtsAccepted();
        second.RecordCtsRejected([("CTWS014", "Invalid Breed")]);

        Assert.True(submission.TryCompleteFromCtsResults(Now));

        Assert.Equal(Statuses.CompleteWithErrors, submission.Status);
    }

    [Fact]
    public void TryCompleteFromCtsResults_WhileAnAnimalIsWaiting_LeavesTheBundle()
    {
        var (submission, first, _) = Bundle();
        first.RecordCtsAccepted();

        Assert.False(submission.TryCompleteFromCtsResults(Now));

        Assert.Equal(Statuses.Submitted, submission.Status);
        Assert.Null(submission.CompletedAt);
    }

    [Fact]
    public void TryCompleteFromCtsResults_Twice_KeepsTheFirstCompletionTime()
    {
        var (submission, first, second) = Bundle();
        first.RecordCtsAccepted();
        second.RecordCtsAccepted();
        submission.TryCompleteFromCtsResults(Now);

        Assert.False(submission.TryCompleteFromCtsResults(Now.AddMinutes(1)));

        Assert.Equal(Now, submission.CompletedAt);
    }

    [Fact]
    public void TryCompleteFromCtsResults_WithoutAnimals_DoesNotComplete() =>
        Assert.False(new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user").TryCompleteFromCtsResults(Now));

    [Fact]
    public void RecordCtsRejected_StoresEachDistinctCauseOnce()
    {
        var (_, animal, _) = Bundle();

        Assert.True(animal.RecordCtsRejected([("CTWS014", "Invalid Breed"), ("CTWS014", "Invalid Breed"), ("CTWS003", "Missing Ear Tag")]));

        Assert.Equal(Statuses.Error, animal.Status);
        Assert.Equal(["CTWS014", "CTWS003"], animal.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public void RecordCtsRejected_Again_AddsNoMoreErrors()
    {
        var (_, animal, _) = Bundle();
        animal.RecordCtsRejected([("CTWS014", "Invalid Breed")]);

        Assert.False(animal.RecordCtsRejected([("CTWS014", "Invalid Breed")]));

        Assert.Single(animal.Errors);
    }

    [Fact]
    public void RecordCtsRejected_WithoutACause_StillRecordsTheFailure()
    {
        var (_, animal, _) = Bundle();

        animal.RecordCtsRejected([]);

        Assert.Equal("CTS_REJECTED", Assert.Single(animal.Errors).ErrorCode);
    }

    [Fact]
    public void RecordCtsRejected_WithACodeButNoText_UsesTheCodeAsTheText()
    {
        var (_, animal, _) = Bundle();

        animal.RecordCtsRejected([("CTWS014", " ")]);

        Assert.Equal("CTWS014", Assert.Single(animal.Errors).ErrorText);
    }

    [Fact]
    public void RecordCtsAccepted_AfterARejection_KeepsTheRejection()
    {
        var (_, animal, _) = Bundle();
        animal.RecordCtsRejected([("CTWS014", "Invalid Breed")]);

        Assert.False(animal.RecordCtsAccepted());

        Assert.Equal(Statuses.Error, animal.Status);
        Assert.Single(animal.Errors);
    }

    [Fact]
    public void MarkAsCtsResultsFailed_KeepsTheReceiptAndRecordsTheError()
    {
        var (submission, _, _) = Bundle();
        submission.RecordCtsRequest("txn", "op", "<x/>");
        submission.MarkAsSentToCts(42, Now.AddDays(-2));

        submission.MarkAsCtsResultsFailed("CTWS002", "Receipt not found", Now);

        Assert.Equal(Statuses.Error, submission.Status);
        Assert.Equal("CTWS002", submission.ErrorCode);
        Assert.Equal(42, submission.CtsReceiptNumber);
        Assert.Equal(Now, submission.UpdatedAt);
        Assert.Null(submission.CompletedAt);
    }

    [Fact]
    public void RecordCtsPoll_RecordsWhenCtsWasAsked()
    {
        var (submission, _, _) = Bundle();

        submission.RecordCtsPoll(Now);

        Assert.Equal(Now, submission.CtsLastPolledAt);
    }

    private static (Submission Submission, SubmissionAnimal First, SubmissionAnimal Second) Bundle()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        return (submission, submission.AddAnimal("UK123456100001"), submission.AddAnimal("UK123456100002"));
    }
}
