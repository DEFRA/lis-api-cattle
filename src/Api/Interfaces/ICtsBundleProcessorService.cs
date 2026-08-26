// <copyright file="ICtsBundleProcessorService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

public interface ICtsBundleProcessorService
{
    Task ProcessPendingBundlesAsync(CancellationToken cancellationToken = default);
}
