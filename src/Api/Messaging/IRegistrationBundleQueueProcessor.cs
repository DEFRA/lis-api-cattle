// <copyright file="IRegistrationBundleQueueProcessor.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

/// <summary>
/// Reads one batch of registration bundles from the queue.
/// </summary>
public interface IRegistrationBundleQueueProcessor
{
    /// <summary>
    /// Receives and handles one batch of messages.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of messages handled and deleted.</returns>
    Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default);
}
