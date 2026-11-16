// <copyright file="ISubmissionLock.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

public interface ISubmissionLock : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the submission exists (and is now locked).
    /// </summary>
    bool Found { get; }

    /// <summary>
    /// Commits the changes saved under the lock and releases the row.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
