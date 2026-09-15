// <copyright file="IKrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;

public interface IKrdsService
{
    /// <summary>
    /// Gets the holding details for a county/parish/holding number from the keeper data service.
    /// </summary>
    /// <exception cref="ArgumentException">A CPH segment does not match the expected format.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The CPH is not known.</exception>
    Task<HoldingResponse> GetHoldingAsync(string county, string parish, string holding, CancellationToken cancellationToken = default);
}
