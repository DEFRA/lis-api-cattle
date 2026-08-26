// <copyright file="SubmissionValidationMessage.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public class SubmissionValidationMessage
{
    public Guid SubmissionId { get; set; }

    public string CountyParishHolding { get; set; } = string.Empty;

    public string ClientReference { get; set; } = string.Empty;

    public string SubmittedBy { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public int AnimalCount { get; set; }
}
