// <copyright file="IKrdsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;

public interface IKrdsService
{
    /// <summary>
    /// Gets the holding details for a county/parish/holding number from the keeper data service.
    /// </summary>
    /// <exception cref="ArgumentException">A CPH segment does not match the expected format.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The CPH is not known.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<HoldingResponse> GetHoldingAsync(string county, string parish, string holding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's details and associated CPHs from the keeper data service, by identity provider subject.
    /// The associations are those captured when the user last signed in; no refresh is performed.
    /// </summary>
    /// <exception cref="ArgumentException">The subject is blank or was rejected by the keeper data service.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.NotFoundException">The subject is not known.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<UserDetailsResponse> GetUserAccountAsync(string subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a user account exists in the keeper data service for the supplied identity provider claims: KRDS
    /// refreshes an existing account (matched by subject, or by an email with no subject yet) or creates one.
    /// </summary>
    /// <param name="request">The identity provider claims (sub, email, given_name, family_name).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentException">A claim is missing or invalid, or was rejected by the keeper data service.</exception>
    /// <exception cref="Defra.Lis.Core.Exceptions.ConflictException">The email belongs to a different account.</exception>
    /// <exception cref="Defra.Lis.Api.Exceptions.ServiceUnavailableException">The keeper data service is temporarily unavailable.</exception>
    /// <returns>The user's details and associated CPHs.</returns>
    Task<UserDetailsResponse> EnsureUserAccountAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
}
