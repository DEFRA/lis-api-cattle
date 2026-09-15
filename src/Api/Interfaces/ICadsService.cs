// <copyright file="ICadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;

public interface ICadsService
{
    /// <summary>
    /// Gets the live animals recorded against a holding in CADS.
    /// </summary>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The holding is not known to CADS.</exception>
    Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph, CancellationToken cancellationToken = default);
}
