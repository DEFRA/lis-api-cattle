// <copyright file="ICtsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;
using Defra.Lis.Entities;

public interface ICtsService
{
    Task<CtsAnimalStatusResponse> SubmitAnimalRegistrationAsync(SubmissionAnimal animal, CancellationToken cancellationToken = default);

    Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default);
}
