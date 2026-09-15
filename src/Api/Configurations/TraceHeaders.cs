// <copyright file="TraceHeaders.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

/// <summary>
/// Header names used to trace a request across services (Correlation ID standard).
/// </summary>
public static class TraceHeaders
{
    public const string CdpRequestId = "x-cdp-request-id";
}
