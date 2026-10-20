// <copyright file="PostgresSubmissionBatchClaimer.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Claims submissions with <c>SELECT ... FOR UPDATE SKIP LOCKED</c>: the rows stay locked by this
/// instance's transaction, and other instances skip them instead of waiting.
/// </summary>
public sealed class PostgresSubmissionBatchClaimer : ISubmissionBatchClaimer
{
    public async Task<ISubmissionBatchClaim> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        IReadOnlyCollection<string> statuses,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var statusArray = statuses.ToArray();
            var ids = await dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value"
                    FROM public.submissions
                    WHERE status = ANY({statusArray})
                    ORDER BY created_at
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            return new Claim(transaction, ids);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Claim(IDbContextTransaction transaction, IReadOnlyList<Guid> submissionIds) : ISubmissionBatchClaim
    {
        public IReadOnlyList<Guid> SubmissionIds { get; } = submissionIds;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
