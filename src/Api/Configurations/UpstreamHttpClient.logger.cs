// <copyright file="UpstreamHttpClient.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Microsoft.Extensions.Logging;

/// <summary>
/// The circuit transition log messages for <see cref="UpstreamHttpClient"/>.
/// </summary>
public static partial class UpstreamHttpClient
{
    [LoggerMessage(LogLevel.Warning, "The {Upstream} circuit opened: at least {FailureCount} calls failed without a response and none succeeded. Calls fail fast for {BreakSeconds}s.")]
    private static partial void LogCircuitOpened(ILogger logger, string upstream, int failureCount, double breakSeconds);

    [LoggerMessage(LogLevel.Warning, "The {Upstream} circuit reopened: the probe call failed without a response. Calls fail fast for {BreakSeconds}s.")]
    private static partial void LogCircuitReopened(ILogger logger, string upstream, double breakSeconds);

    [LoggerMessage(LogLevel.Information, "The {Upstream} circuit is half-open: letting one probe call through.")]
    private static partial void LogCircuitHalfOpened(ILogger logger, string upstream);

    [LoggerMessage(LogLevel.Information, "The {Upstream} circuit closed: calls are being answered again.")]
    private static partial void LogCircuitClosed(ILogger logger, string upstream);
}
