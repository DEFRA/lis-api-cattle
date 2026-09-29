// <copyright file="SubmissionValidationBackgroundService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class SubmissionValidationBackgroundService
{
    [LoggerMessage(LogLevel.Information, "SubmissionValidationBackgroundService is disabled.")]
    partial void LogSubmissionvalidationbackgroundserviceIsDisabled();

    [LoggerMessage(LogLevel.Information, "SubmissionValidationBackgroundService started.")]
    partial void LogSubmissionvalidationbackgroundserviceStarted();

    [LoggerMessage(LogLevel.Error, "Unhandled error occurred in SubmissionValidationBackgroundService polling loop.")]
    partial void LogUnhandledErrorOccurredInSubmissionvalidationbackgroundservicePollingLoop(Exception exception);

    [LoggerMessage(LogLevel.Information, "SubmissionValidationBackgroundService stopped.")]
    partial void LogSubmissionvalidationbackgroundserviceStopped();
}
