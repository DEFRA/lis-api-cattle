// <copyright file="CtsBundlePollingJob.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Jobs;

public partial class CtsBundlePollingJob
{
    [LoggerMessage(LogLevel.Information, "Starting CTS bundle polling job execution at {Time}")]
    partial void LogStartingCtsBundlePollingJobExecutionAtTime(DateTimeOffset time);

    [LoggerMessage(LogLevel.Information, "Finished CTS bundle polling job execution successfully at {Time}")]
    partial void LogFinishedCtsBundlePollingJobExecutionSuccessfullyAtTime(DateTimeOffset time);

    [LoggerMessage(LogLevel.Error, "CTS bundle polling job execution failed.")]
    partial void LogCtsBundlePollingJobExecutionFailed(Exception exception);
}
