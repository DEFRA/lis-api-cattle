// <copyright file="RegistrationBundleMessage.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using Defra.Lis.Api.Models.Requests;

/// <summary>
/// The SQS message that carries an accepted registration bundle to the queue worker.
/// </summary>
public sealed class RegistrationBundleMessage
{
    /// <summary>The name of the SQS message attribute that carries the correlation ID (Correlation ID standard).</summary>
    public const string CorrelationIdAttribute = "CorrelationId";

    /// <summary>Gets or sets the submission ID the API gave the bundle. It is the idempotency key.</summary>
    public Guid SubmissionId { get; set; }

    /// <summary>Gets or sets the correlation ID of the request that submitted the bundle.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets when the API accepted the bundle.</summary>
    public DateTimeOffset AcceptedAt { get; set; }

    /// <summary>Gets or sets the bundle exactly as the caller sent it.</summary>
    public RegistrationBundleRequest Bundle { get; set; } = null!;
}
