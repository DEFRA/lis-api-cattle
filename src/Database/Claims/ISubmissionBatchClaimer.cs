// <copyright file="ISubmissionBatchClaimer.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

/// <summary>
/// Claims a batch of submissions so that several instances of the service process separate rows.
/// </summary>
public interface ISubmissionBatchClaimer
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> of the oldest submissions in one of <paramref name="statuses"/>
    /// that no other instance holds, inside a transaction on <paramref name="dbContext"/>.
    /// </summary>
    /// <remarks>
    /// The transaction is opened here, so a context with a retrying execution strategy must call this, and do all
    /// the work under the claim, inside <c>dbContext.Database.CreateExecutionStrategy().ExecuteAsync</c>.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<ISubmissionBatchClaim> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        IReadOnlyCollection<string> statuses,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> bundles that were sent to CTS (status <c>submitted</c> with a stored
    /// receipt) and are not yet completed, that no other instance holds. Bundles polled longest ago, or never, come
    /// first, so a bundle CTS keeps reporting as not ready does not hold back the others.
    /// </summary>
    /// <remarks>The same transaction rules apply as for <see cref="ClaimAsync"/>.</remarks>
    /// <param name="dbContext">The context whose transaction holds the claim.</param>
    /// <param name="batchSize">The most bundles to claim.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<ISubmissionBatchClaim> ClaimAwaitingCtsResultsAsync(
        DbContext dbContext,
        int batchSize,
        CancellationToken cancellationToken = default);
}
