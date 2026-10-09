// <copyright file="ICadsDataPatchStore.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models.Cads;

/// <summary>
/// Reads the temporary CADS data patch: animals held per CPH that CADS does not yet return.
/// </summary>
public interface ICadsDataPatchStore
{
    /// <summary>
    /// Lists the ear tags held for a holding.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<IReadOnlyList<string>> GetEarTagsAsync(string cph, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one animal held for a holding, or null when the file is missing or not a valid animal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<CadsAnimal?> GetAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the animal details.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<CadsAnimalDetail?> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default);
}
