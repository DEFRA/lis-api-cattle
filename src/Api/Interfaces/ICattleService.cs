// <copyright file="ICattleService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;

public interface ICattleService
{
    Task<IEnumerable<CattleResponse>> GetCattleForHoldingAsync(string cph);

    Task<IEnumerable<BundleResponse>> GetBundlesForHoldingAsync(string cph);

    Task<BundleResponse> CreateRegistrationBundleAsync(RegistrationBundleRequest request, CancellationToken cancellationToken = default);
}
