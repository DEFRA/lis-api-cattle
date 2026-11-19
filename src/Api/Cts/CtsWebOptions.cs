// <copyright file="CtsWebOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Connection settings for CTS Web Services (the <c>TransferDataHex</c> SOAP endpoint). The credentials are CDP
/// secrets. Locally the endpoint is lis-fake-service's <c>/cts_ws</c> fake.
/// </summary>
public sealed class CtsWebOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "CtsWeb";

    /// <summary>Gets the full URL of <c>DefraDataTransferPublicNWSE.asmx</c>.</summary>
    [Required]
    public string Url { get; init; } = string.Empty;

    /// <summary>Gets the CTS service name sent in the TransferDataHex envelope.</summary>
    [Required]
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>Gets the TransferDataHex (DTH) user name.</summary>
    [Required]
    public string DthUsername { get; init; } = string.Empty;

    /// <summary>Gets the TransferDataHex (DTH) password. It is sent MD5-hashed, as the real service requires.</summary>
    [Required]
    public string DthPassword { get; init; } = string.Empty;

    /// <summary>Gets the CTS Online system account user name (one account for every bundle).</summary>
    [Required]
    [MaxLength(11)]
    public string CtsOnlineUsername { get; init; } = string.Empty;

    /// <summary>Gets the CTS Online system account password.</summary>
    [Required]
    [MaxLength(15)]
    public string CtsOnlinePassword { get; init; } = string.Empty;

    /// <summary>Gets the timeout for one CTS call, in milliseconds.</summary>
    [Range(1, int.MaxValue)]
    public int TimeoutMilliseconds { get; init; } = 30_000;

    /// <summary>Gets how many times one bundle's request is tried when CTS is unavailable, before the bundle is marked as errored.</summary>
    [Range(1, 10)]
    public int MaxAttempts { get; init; } = 3;

    /// <summary>Gets the delay between attempts, in milliseconds. It doubles after each attempt.</summary>
    [Range(0, int.MaxValue)]
    public int RetryDelayMilliseconds { get; init; } = 1_000;
}
