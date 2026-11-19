// <copyright file="IRegistrationBundlePublisher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

/// <summary>
/// Puts accepted registration bundles on the registration bundle queue.
/// </summary>
public interface IRegistrationBundlePublisher
{
    /// <summary>
    /// Sends the bundle to the queue.
    /// </summary>
    /// <param name="message">The bundle message.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once SQS has accepted the message.</returns>
    /// <exception cref="Exceptions.ServiceUnavailableException">SQS is not configured or could not take the message.</exception>
    Task PublishAsync(RegistrationBundleMessage message, CancellationToken cancellationToken = default);
}
