// <copyright file="RegistrationBundleHandler.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class RegistrationBundleHandler
{
    [LoggerMessage(LogLevel.Information, "Stored registration bundle {SubmissionId} with {AnimalCount} animals and its CTS Birth request")]
    partial void LogBundleStored(Guid submissionId, int animalCount);

    [LoggerMessage(LogLevel.Information, "Created {CreatedCount} pending animal records for registration bundle {SubmissionId}; {RejectedCount} animals were rejected because their ear tag is missing, repeated or already allocated")]
    partial void LogAnimalRecordsCreated(Guid submissionId, int createdCount, int rejectedCount);

    [LoggerMessage(LogLevel.Information, "Registration bundle {SubmissionId} is already stored")]
    partial void LogBundleAlreadyStored(Guid submissionId);

    [LoggerMessage(LogLevel.Information, "Registration bundle {SubmissionId} already has a CTS outcome (status {Status}), so it is not sent again")]
    partial void LogBundleAlreadyHasCtsOutcome(Guid submissionId, string status);

    [LoggerMessage(LogLevel.Information, "Sent registration bundle {SubmissionId} to CTS. Receipt number: {ReceiptNumber}")]
    partial void LogBundleSentToCts(Guid submissionId, long receiptNumber);

    [LoggerMessage(LogLevel.Warning, "CTS did not accept registration bundle {SubmissionId} ({ErrorCode}: {ErrorText}, transient: {IsTransient}). The bundle is marked as errored")]
    partial void LogBundleRejectedByCts(Guid submissionId, string? errorCode, string? errorText, bool isTransient);

    [LoggerMessage(LogLevel.Error, "CTS reports registration bundle {SubmissionId} (TxnId {TxnId}) as already submitted, but no receipt was stored. The bundle is marked as errored and needs reconciling")]
    partial void LogBundleNeedsReconciliation(Guid submissionId, string txnId);
}
