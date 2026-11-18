// <copyright file="PostgresSubmissionBatchClaimer.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Claims;

using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Claims submissions with <c>SELECT ... FOR UPDATE SKIP LOCKED</c>: the rows stay locked by this
/// instance's transaction, and other instances skip them instead of waiting.
/// </summary>
public sealed class PostgresSubmissionBatchClaimer : ISubmissionBatchClaimer
{
    public Task<ISubmissionBatchClaim> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        IReadOnlyCollection<string> statuses,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var statusArray = statuses.ToArray();
        return ClaimWhereAsync(
            dbContext,
            db => db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value"
                FROM public.submissions
                WHERE status = ANY({statusArray})
                ORDER BY created_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """),
            cancellationToken);
    }

    public Task<ISubmissionBatchClaim> ClaimAwaitingCtsResultsAsync(
        DbContext dbContext,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        const string submitted = Statuses.Submitted;
        return ClaimWhereAsync(
            dbContext,
            db => db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value"
                FROM public.submissions
                WHERE status = {submitted}
                  AND cts_receipt_number IS NOT NULL
                  AND completed_at IS NULL
                ORDER BY cts_last_polled_at NULLS FIRST, created_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """),
            cancellationToken);
    }

    private static async Task<ISubmissionBatchClaim> ClaimWhereAsync(
        DbContext dbContext,
        Func<DbContext, IQueryable<Guid>> query,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var ids = await query(dbContext).ToListAsync(cancellationToken);
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
