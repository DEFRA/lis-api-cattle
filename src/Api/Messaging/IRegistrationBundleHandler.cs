// <copyright file="IRegistrationBundleHandler.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

/// <summary>
/// Stores a queued registration bundle and sends it to CTS.
/// </summary>
public interface IRegistrationBundleHandler
{
    /// <summary>
    /// Handles one queued bundle. Safe to call more than once for the same message.
    /// </summary>
    /// <param name="message">The queued bundle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The outcome. Once this returns, the message can be deleted.</returns>
    /// <exception cref="Exception">The bundle could not be stored or its outcome saved. The message should be retried.</exception>
    Task<RegistrationBundleOutcome> HandleAsync(RegistrationBundleMessage message, CancellationToken cancellationToken = default);
}
