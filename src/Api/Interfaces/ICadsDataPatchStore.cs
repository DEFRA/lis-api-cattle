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
    /// Lists the cphs in the bucket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<IReadOnlyList<string>> GetCphListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the ear tags held for a holding.
    /// </summary>
    /// <param name="cph">The county/parish/holding to scope this request to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<IReadOnlyList<string>> GetCphEarTagsAsync(string cph, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads animal details from any holding in the patch, or null when missing or invalid.
    /// </summary>
    /// <returns>The animal details, when available.</returns>
    Task<CadsAnimalDetail?> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one animal held for a holding, or null when the file is missing or not a valid animal.
    /// </summary>
    /// <param name="cph">The county/parish/holding to scope this request to.</param>
    /// <param name="earTag">The eartag of the animal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<CadsAnimal?> GetAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add or replace an animal data file from the specified CPH.
    /// </summary>
    /// <param name="cph">The county/parish/holding to scope this request to.</param>
    /// <param name="earTag">The ear tag of the animal.</param>
    /// <param name="json">The data of the animal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<bool> UpsertAnimalAsync(string cph, string earTag, string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove an animal data file from the specified CPH.
    /// </summary>
    /// <param name="cph">The county/parish/holding to scope this request to.</param>
    /// <param name="earTag">The ear tag of the animal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<bool> DeleteAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default);
}
