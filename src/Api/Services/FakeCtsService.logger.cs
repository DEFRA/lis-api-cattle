// <copyright file="FakeCtsService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

public partial class FakeCtsService
{
    [LoggerMessage(LogLevel.Information, "Fake CTS received submission for animal {AnimalId} with EarTag {EarTag}")]
    partial void LogFakeCtsReceivedSubmissionForAnimalAnimalidWithEartagEartag(Guid animalId, string earTag);

    [LoggerMessage(LogLevel.Information, "Fake CTS checking status for animal {AnimalId} with EarTag {EarTag}")]
    partial void LogFakeCtsCheckingStatusForAnimalAnimalidWithEartagEartag(Guid animalId, string earTag);
}
