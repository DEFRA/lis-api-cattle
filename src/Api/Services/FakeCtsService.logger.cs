// <copyright file="FakeCtsService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

public partial class FakeCtsService
{
    [LoggerMessage(LogLevel.Information, "Fake CTS checking status for animal {AnimalId} with EarTag {EarTag}")]
    partial void LogFakeCtsCheckingStatusForAnimalAnimalidWithEartagEartag(Guid animalId, string earTag);
}
