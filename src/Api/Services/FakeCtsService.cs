// <copyright file="FakeCtsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;
using Microsoft.Extensions.Logging;

public class FakeCtsService(
    ILogger<FakeCtsService>? logger = null)
    : ICtsService
{
    public Task<CtsAnimalStatusResponse> SubmitAnimalRegistrationAsync(SubmissionAnimal animal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);

        logger?.LogInformation("Fake CTS received submission for animal {AnimalId} with EarTag {EarTag}", animal.Id, animal.EarTag);

        if (animal.EarTag.Contains("SUBMIT_ERR", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = animal.EarTag,
                Status = Statuses.Error,
                Errors =
                [
                    new CtsErrorResponse
                    {
                        ErrorCode = "CTS_SUBMIT_FAIL",
                        ErrorText = "Submission rejected by CTS validation",
                    },
                ],
            });
        }

        return Task.FromResult(new CtsAnimalStatusResponse
        {
            EarTag = animal.EarTag,
            Status = Statuses.Processing,
        });
    }

    public Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        logger?.LogInformation("Fake CTS checking status for animal {AnimalId} with EarTag {EarTag}", animalId, earTag);

        if (earTag.Contains("ERR", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = earTag,
                Status = Statuses.Error,
                Errors =
                [
                    new CtsErrorResponse
                    {
                        ErrorCode = "CTS_VALIDATION_ERROR",
                        ErrorText = $"CTS issue detected for animal {earTag}",
                    },
                ],
            });
        }

        if (earTag.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = earTag,
                Status = Statuses.Processing,
            });
        }

        return Task.FromResult(new CtsAnimalStatusResponse
        {
            EarTag = earTag,
            Status = "clean",
        });
    }
}
