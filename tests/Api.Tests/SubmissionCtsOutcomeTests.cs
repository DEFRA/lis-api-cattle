// <copyright file="SubmissionCtsOutcomeTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Entities;

public class SubmissionCtsOutcomeTests
{
    [Fact]
    public void Constructor_RejectsAnEmptyId() =>
        Assert.Throws<ArgumentException>(() => new Submission(Guid.Empty, "REF", "10/081/1234", "user"));

    [Fact]
    public void MarkAsSentToCts_WithoutAStoredRequest_Throws()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");

        Assert.Throws<InvalidOperationException>(() => submission.MarkAsSentToCts(1, DateTimeOffset.UtcNow));
        Assert.False(submission.HasCtsOutcome);
    }

    [Fact]
    public void RecordCtsRequest_Twice_Throws()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        submission.RecordCtsRequest("txn", "op", "<x/>");

        Assert.Throws<InvalidOperationException>(() => submission.RecordCtsRequest("txn", "op", "<x/>"));
    }

    [Fact]
    public void MarkAsSentToCts_RecordsTheReceiptOnTheBundleAndItsRequest()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        submission.RecordCtsRequest("txn", "op", "<x/>");
        var sentAt = DateTimeOffset.UtcNow;

        submission.MarkAsSentToCts(42, sentAt);

        Assert.Equal(Statuses.Submitted, submission.Status);
        Assert.Equal(42, submission.CtsReceiptNumber);
        Assert.Equal(sentAt, submission.CtsSentAt);
        Assert.Equal(42, submission.CtsRequest!.ReceiptNumber);
        Assert.Equal(sentAt, submission.CtsRequest.SentAt);
        Assert.True(submission.HasCtsOutcome);
        Assert.NotNull(submission.UpdatedAt);
    }

    [Fact]
    public void MarkAsCtsFailed_LeavesTheBundleErroredAndNotSent()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        submission.RecordCtsRequest("txn", "op", "<x/>");

        submission.MarkAsCtsFailed("CTWS808", "Invalid");

        Assert.Equal(Statuses.Error, submission.Status);
        Assert.Equal("CTWS808", submission.ErrorCode);
        Assert.Equal("Invalid", submission.ErrorText);
        Assert.Null(submission.CtsReceiptNumber);
        Assert.Null(submission.CtsSentAt);
        Assert.Equal("CTWS808: Invalid", submission.CtsRequest!.LastError);
        Assert.True(submission.HasCtsOutcome);
    }

    [Theory]
    [InlineData("", "text")]
    [InlineData("code", " ")]
    public void MarkAsCtsFailed_RequiresACodeAndText(string code, string text)
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");

        Assert.Throws<ArgumentException>(() => submission.MarkAsCtsFailed(code, text));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("corr-1", "corr-1")]
    public void RecordCorrelationId_StoresOnlyARealValue(string? value, string? expected)
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");

        submission.RecordCorrelationId(value);

        Assert.Equal(expected, submission.CorrelationId);
    }

    [Fact]
    public void CtsRequest_RejectsAnEmptySubmissionId() =>
        Assert.Throws<ArgumentException>(() => new CtsRequest(Guid.Empty, "txn", "op", "<x/>"));

    [Fact]
    public void CtsRequest_RejectsATransactionIdLongerThanCtsAllows() =>
        Assert.Throws<ArgumentException>(() => new CtsRequest(Guid.NewGuid(), new string('t', CtsRequest.MaxTxnIdLength + 1), "op", "<x/>"));
}
