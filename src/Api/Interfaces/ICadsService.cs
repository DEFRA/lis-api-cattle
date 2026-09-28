// <copyright file="ICadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;

public interface ICadsService
{
    /// <summary>
    /// Gets the live animals recorded against a holding in CADS.
    /// </summary>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The holding is not known to CADS.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets everything CADS holds about a single animal.
    /// </summary>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The animal is not known to CADS.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<CattleDetailsResponse> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default);
}
