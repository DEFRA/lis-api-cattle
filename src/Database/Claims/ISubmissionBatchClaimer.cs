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
}
