// <copyright file="RegistrationBundleBackgroundService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class RegistrationBundleBackgroundService
{
    [LoggerMessage(LogLevel.Information, "Registration bundle consumer started")]
    partial void LogRegistrationBundleConsumerStarted();

    [LoggerMessage(LogLevel.Information, "Registration bundle consumer stopped")]
    partial void LogRegistrationBundleConsumerStopped();

    [LoggerMessage(LogLevel.Error, "Unhandled error in the registration bundle consumer")]
    partial void LogUnhandledErrorInRegistrationBundleConsumer(Exception exception);
}
