// <copyright file="ApiKeyAuthenticationHandler.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Authentication;

public sealed partial class ApiKeyAuthenticationHandler
{
    [LoggerMessage(LogLevel.Warning, "Rejected request with {Reason} API key for {Method} {Path} (CdpRequestId {CdpRequestId})")]
    private static partial void LogApiKeyRejected(ILogger logger, string reason, string method, PathString path, string cdpRequestId);

    [LoggerMessage(LogLevel.Warning, "No API keys are configured; rejecting request for {Path} (CdpRequestId {CdpRequestId})")]
    private static partial void LogNoApiKeysConfigured(ILogger logger, PathString path, string cdpRequestId);
}
