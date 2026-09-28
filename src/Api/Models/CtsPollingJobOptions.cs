// <copyright file="CtsPollingJobOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

/// <summary>
/// Configuration options for the CTS (Cattle Tracing System) bundle polling background job.
/// </summary>
public class CtsPollingJobOptions
{
    /// <summary>
    /// Configuration section name in application settings.
    /// </summary>
    public const string SectionName = "CtsPollingJob";

    /// <summary>
    /// Gets or sets a value indicating whether the CTS polling job is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the cron schedule expression for running the polling job. If not specified, <see cref="PollingIntervalSeconds"/> is used.
    /// </summary>
    public string? CronSchedule { get; set; }

    /// <summary>
    /// Gets or sets the interval in seconds between polling executions when a cron schedule is not provided.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of pending submission bundles to process in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
