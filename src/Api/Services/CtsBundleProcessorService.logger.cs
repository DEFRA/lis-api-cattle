// <copyright file="CtsBundleProcessorService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

public partial class CtsBundleProcessorService
{
    [LoggerMessage(LogLevel.Debug, "No bundles are waiting for CTS results.")]
    partial void LogNoPendingBundlesFoundToProcess();

    [LoggerMessage(LogLevel.Information, "Polling CTS for the results of {Count} bundles.")]
    partial void LogFoundCountBundlesToProcessWithCts(int count);

    [LoggerMessage(LogLevel.Error, "Error processing bundle {BundleId}")]
    partial void LogErrorProcessingBundleBundleid(Guid bundleId, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Bundles {BundleIds} were changed by another writer while being processed; the batch was rolled back and will be retried")]
    partial void LogBatchConflictedWithAnotherWriter(string bundleIds);

    [LoggerMessage(LogLevel.Warning, "Bundle {BundleId} has a CTS receipt but no stored CTS request, so its results cannot be matched; it was skipped")]
    partial void LogBundleHasNoCtsRequest(Guid bundleId);

    [LoggerMessage(LogLevel.Information, "CTS results for bundle {BundleId} (receipt {ReceiptNumber}) are not ready yet")]
    partial void LogCtsResultsNotReady(Guid bundleId, long receiptNumber);

    [LoggerMessage(LogLevel.Warning, "Could not get CTS results for bundle {BundleId} (receipt {ReceiptNumber}): {ErrorCode} {ErrorText} (transient: {IsTransient}); it will be polled again")]
    partial void LogCtsResultsFailed(Guid bundleId, long receiptNumber, string? errorCode, string? errorText, bool isTransient);

    [LoggerMessage(LogLevel.Error, "CTS does not recognise receipt {ReceiptNumber} for bundle {BundleId} after the allowed time; the bundle was marked as errored")]
    partial void LogBundleReceiptUnknown(Guid bundleId, long receiptNumber);

    [LoggerMessage(LogLevel.Error, "CTS returned results for another transaction for bundle {BundleId} (receipt {ReceiptNumber}); they were ignored")]
    partial void LogCtsResultsForAnotherTransaction(Guid bundleId, long receiptNumber);

    [LoggerMessage(LogLevel.Warning, "CTS result row {RowNum} for bundle {BundleId} does not match one animal in the stored request; it was ignored")]
    partial void LogCtsRowNotMatched(Guid bundleId, int rowNum);

    [LoggerMessage(LogLevel.Information, "Bundle {BundleId} completed as {Status} ({AcceptedCount} accepted and {RejectedCount} rejected rows in the last CTS response)")]
    partial void LogBundleCompleted(Guid bundleId, string status, int acceptedCount, int rejectedCount);

    [LoggerMessage(LogLevel.Information, "Bundle {BundleId}: {UpdatedCount} animals updated from CTS, {OutstandingCount} still waiting")]
    partial void LogBundlePartiallyProcessed(Guid bundleId, int updatedCount, int outstandingCount);
}
