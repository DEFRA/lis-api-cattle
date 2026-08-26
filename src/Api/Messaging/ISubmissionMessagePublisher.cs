// <copyright file="ISubmissionMessagePublisher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public interface ISubmissionMessagePublisher
{
    Task PublishSubmissionForValidationAsync(SubmissionValidationMessage message, CancellationToken cancellationToken = default);
}
