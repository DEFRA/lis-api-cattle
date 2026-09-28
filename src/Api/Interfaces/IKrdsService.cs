// <copyright file="IKrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;

public interface IKrdsService
{
    /// <summary>
    /// Gets the holding details for a county/parish/holding number from the keeper data service.
    /// </summary>
    /// <exception cref="ArgumentException">A CPH segment does not match the expected format.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The CPH is not known.</exception>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<HoldingResponse> GetHoldingAsync(string county, string parish, string holding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's details and associated CPHs from the keeper data service, by identity provider subject.
    /// The associations are those captured when the user last signed in; no refresh is performed.
    /// </summary>
    /// <exception cref="ArgumentException">The subject is blank or was rejected by the keeper data service.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The subject is not known.</exception>
    Task<UserDetailsResponse> GetUserAccountAsync(string subject, CancellationToken cancellationToken = default);
}
