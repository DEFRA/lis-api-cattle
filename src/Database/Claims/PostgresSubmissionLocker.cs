// <copyright file="PostgresSubmissionLocker.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

using Microsoft.EntityFrameworkCore.Storage;

public sealed class PostgresSubmissionLocker : ISubmissionLocker
{
    public async Task<ISubmissionLock> LockAsync(DbContext dbContext, Guid submissionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // FOR UPDATE (without SKIP LOCKED) waits for another instance's lock, then sees what it committed.
            var ids = await dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value"
                    FROM public.submissions
                    WHERE id = {submissionId}
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken);

            return new Lock(transaction, ids.Count == 1);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Lock(IDbContextTransaction transaction, bool found) : ISubmissionLock
    {
        public bool Found { get; } = found;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
