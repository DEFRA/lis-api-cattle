// <copyright file="CtsBundleProcessorService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

public partial class CtsBundleProcessorService
{
    [LoggerMessage(LogLevel.Debug, "No pending bundles found to process.")]
    partial void LogNoPendingBundlesFoundToProcess();

    [LoggerMessage(LogLevel.Information, "Found {Count} bundles to process with CTS.")]
    partial void LogFoundCountBundlesToProcessWithCts(int count);

    [LoggerMessage(LogLevel.Error, "Error processing bundle {BundleId}")]
    partial void LogErrorProcessingBundleBundleid(Guid bundleId, Exception exception);
}
