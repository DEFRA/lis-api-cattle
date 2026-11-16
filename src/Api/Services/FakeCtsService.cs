// <copyright file="FakeCtsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class FakeCtsService(
    ILogger<FakeCtsService>? logger)
    : ICtsService
{
    private readonly ILogger logger = logger ?? NullLogger<FakeCtsService>.Instance;

    public Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        LogFakeCtsCheckingStatusForAnimalAnimalidWithEartagEartag(animalId, earTag);

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
