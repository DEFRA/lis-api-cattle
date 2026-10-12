// <copyright file="CorrelationIdMiddleware.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Correlation;

public sealed partial class CorrelationIdMiddleware
{
    [LoggerMessage(LogLevel.Warning, "Rejected request without a single x-cdp-request-id header for {Method} {Path}")]
    partial void LogMissingCorrelationId(string method, PathString path);
}
