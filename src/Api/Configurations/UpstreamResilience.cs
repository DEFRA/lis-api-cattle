// <copyright file="UpstreamResilience.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

/// <summary>
/// The timeout and circuit breaker settings for one upstream.
/// </summary>
/// <param name="Upstream">The upstream's name in the transition logs.</param>
/// <param name="Timeout">How long a call may wait for the response headers.</param>
/// <param name="FailureThreshold">How many calls must fail without a response, with none succeeding, to open the circuit.</param>
/// <param name="SamplingDuration">The window over which failures are counted.</param>
/// <param name="BreakDuration">How long calls fail fast once the circuit opens.</param>
public sealed record UpstreamResilience(
    string Upstream,
    TimeSpan Timeout,
    int FailureThreshold,
    TimeSpan SamplingDuration,
    TimeSpan BreakDuration);
