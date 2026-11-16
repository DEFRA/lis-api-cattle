// <copyright file="CtsWebClient.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

public partial class CtsWebClient
{
    [LoggerMessage(LogLevel.Warning, "CTS attempt {Attempt} of {MaxAttempts} failed with {ErrorCode}: {ErrorText}")]
    partial void LogCtsAttemptFailed(int attempt, int maxAttempts, string? errorCode, string? errorText);

    [LoggerMessage(LogLevel.Warning, "CTS could not be reached")]
    partial void LogCtsCouldNotBeReached(Exception exception);
}
