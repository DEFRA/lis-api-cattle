// <copyright file="RegistrationBundleOutcome.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

/// <summary>
/// What happened to a queued registration bundle.
/// </summary>
public enum RegistrationBundleOutcome
{
    /// <summary>The bundle was sent to CTS and the receipt stored.</summary>
    Sent,

    /// <summary>CTS rejected the bundle or could not be reached, and the bundle was marked as errored.</summary>
    Failed,

    /// <summary>The bundle already had a CTS outcome (a redelivered message), so nothing was done.</summary>
    AlreadyHandled,
}
