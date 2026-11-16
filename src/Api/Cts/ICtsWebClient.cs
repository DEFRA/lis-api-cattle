// <copyright file="ICtsWebClient.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>
/// Sends requests to CTS Web Services.
/// </summary>
public interface ICtsWebClient
{
    /// <summary>
    /// Sends a stored CTSWeb request, adding the CTS Online credentials, and returns the receipt or the error.
    /// Transient failures are retried up to <see cref="CtsWebOptions.MaxAttempts"/> times before a failure is returned.
    /// </summary>
    /// <param name="operation">The CTSWeb operation (TransferDataHex <c>type</c>).</param>
    /// <param name="payload">The stored request XML, without its <c>Authentication</c> element.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The outcome. A failure is returned, not thrown.</returns>
    Task<CtsSubmitResult> SubmitAsync(string operation, string payload, CancellationToken cancellationToken = default);
}
