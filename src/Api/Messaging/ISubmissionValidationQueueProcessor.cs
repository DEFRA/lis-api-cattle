// <copyright file="ISubmissionValidationQueueProcessor.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public interface ISubmissionValidationQueueProcessor
{
    Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default);
}
