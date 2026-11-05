// <copyright file="CadsDataPatchOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Settings for the temporary CADS data patch: an S3 bucket holding one folder per CPH, each containing
/// a <c>{earTag}.json</c> file per animal that CADS does not yet return for that holding.
/// </summary>
public sealed class CadsDataPatchOptions
{
    public const string SectionName = "CadsDataPatch";

    /// <summary>
    /// Gets the bucket holding the patch files. When blank the patch is switched off.
    /// </summary>
    public string? BucketName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the patch is switched on.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(BucketName);

    /// <summary>
    /// Gets how long one S3 request for the patch may take, so a slow bucket cannot use up the request's budget.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 2;

    /// <summary>
    /// Gets how many times the S3 client retries a failed patch request.
    /// </summary>
    [Range(0, 10)]
    public int MaxErrorRetry { get; init; } = 1;
}
