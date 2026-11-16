// <copyright file="ISubmissionLocker.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

public interface ISubmissionLocker
{
    /// <summary>
    /// Locks one submission row inside a transaction on <paramref name="dbContext"/>, waiting for any other
    /// instance that holds it, so that only one instance at a time acts on the submission.
    /// </summary>
    /// <remarks>
    /// The transaction is opened here, so a context with a retrying execution strategy must call this, and do all
    /// the work under the lock, inside <c>dbContext.Database.CreateExecutionStrategy().ExecuteAsync</c>.
    /// </remarks>
    /// <param name="dbContext">The context whose transaction holds the lock.</param>
    /// <param name="submissionId">The submission to lock.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<ISubmissionLock> LockAsync(DbContext dbContext, Guid submissionId, CancellationToken cancellationToken = default);
}
