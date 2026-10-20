// <copyright file="ISubmissionBatchClaim.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

/// <summary>
/// A batch of submissions claimed by this instance. Other instances skip the claimed rows until the
/// claim is committed or disposed; disposing without committing rolls back every change made under it.
/// </summary>
public interface ISubmissionBatchClaim : IAsyncDisposable
{
    /// <summary>
    /// Gets the identifiers of the claimed submissions, oldest first.
    /// </summary>
    IReadOnlyList<Guid> SubmissionIds { get; }

    /// <summary>
    /// Commits the changes saved under the claim and releases the claimed rows.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
