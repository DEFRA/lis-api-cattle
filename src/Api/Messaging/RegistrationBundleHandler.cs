// <copyright file="RegistrationBundleHandler.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using Defra.Database.Postgres;
using Defra.Lis.Api.Cts;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stores a queued registration bundle and sends it to CTS as one Birth request. It is safe to run for the same
/// message more than once, and on several instances at the same time.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The bundle, its animals and the CTS request are stored in one transaction, keyed by the submission ID that
/// the API gave the bundle, so a redelivered message does not store a second bundle.</item>
/// <item>The submission row is locked, and the stored request is sent only when the bundle has no outcome yet, so
/// two deliveries cannot both send it. The receipt, or the error, is saved before the lock is released.</item>
/// </list>
/// A database failure is thrown, so that the message is left on the queue and retried. CTS failures are not
/// retried: the bundle is marked as errored with the CTS error (LREG-925).
/// </remarks>
public partial class RegistrationBundleHandler(
    DbContext dbContext,
    ISubmissionLocker locker,
    ICtsWebClient ctsWebClient,
    TimeProvider timeProvider,
    ILogger<RegistrationBundleHandler> logger)
    : IRegistrationBundleHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationBundleHandler"/> class with the read-write context.
    /// </summary>
    /// <param name="dbContext">The read-write Postgres context.</param>
    /// <param name="locker">Locks a submission row.</param>
    /// <param name="ctsWebClient">The CTSWeb client.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public RegistrationBundleHandler(
        PostgresDbContext dbContext,
        ISubmissionLocker locker,
        ICtsWebClient ctsWebClient,
        TimeProvider timeProvider,
        ILogger<RegistrationBundleHandler> logger)
        : this((DbContext)dbContext, locker, ctsWebClient, timeProvider, logger)
    {
    }

    /// <inheritdoc />
    public async Task<RegistrationBundleOutcome> HandleAsync(RegistrationBundleMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Bundle);

        // A retrying execution strategy rejects a transaction it does not own, so each step runs as one retriable unit.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(ct => StoreAsync(message, ct), cancellationToken);
        return await strategy.ExecuteAsync(ct => SendAsync(message.SubmissionId, ct), cancellationToken);
    }

    private async Task StoreAsync(RegistrationBundleMessage message, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        if (await dbContext.Set<Submission>().AnyAsync(s => s.Id == message.SubmissionId, cancellationToken))
        {
            LogBundleAlreadyStored(message.SubmissionId);
            return;
        }

        var submission = RegistrationBundleMapper.ToSubmission(message);
        submission.RecordCtsRequest(
            CtsBirthRequestBuilder.TxnIdFor(submission),
            CtsBirthRequestBuilder.Operation,
            CtsBirthRequestBuilder.Build(submission, timeProvider.GetUtcNow()));

        await dbContext.Set<Submission>().AddAsync(submission, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            LogBundleStored(submission.Id, submission.Animals.Count);
        }
        catch (DbUpdateException)
        {
            if (!await StoredByAnotherInstanceAsync(message.SubmissionId, cancellationToken))
            {
                throw;
            }

            // Another instance stored the same message first (a redelivery racing this one).
            LogBundleAlreadyStored(message.SubmissionId);
        }
    }

    private async Task<bool> StoredByAnotherInstanceAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        return await dbContext.Set<Submission>().AnyAsync(s => s.Id == submissionId, cancellationToken);
    }

    private async Task<RegistrationBundleOutcome> SendAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var submissionLock = await locker.LockAsync(dbContext, submissionId, cancellationToken);
        if (!submissionLock.Found)
        {
            throw new InvalidOperationException($"Submission {submissionId} was not found after it was stored.");
        }

        var submission = await dbContext.Set<Submission>()
            .Include(s => s.CtsRequest)
            .SingleAsync(s => s.Id == submissionId, cancellationToken);

        if (submission.HasCtsOutcome)
        {
            // A redelivered message for a bundle that was already sent (or failed): nothing more to do.
            LogBundleAlreadyHasCtsOutcome(submissionId, submission.Status);
            await submissionLock.CommitAsync(cancellationToken);
            return RegistrationBundleOutcome.AlreadyHandled;
        }

        var request = submission.CtsRequest
            ?? throw new InvalidOperationException($"Submission {submissionId} has no stored CTS request.");

        var result = await ctsWebClient.SubmitAsync(request.Operation, request.Payload, cancellationToken);

        RegistrationBundleOutcome outcome;
        if (result.IsAccepted)
        {
            submission.MarkAsSentToCts(result.ReceiptNumber!.Value, timeProvider.GetUtcNow());
            outcome = RegistrationBundleOutcome.Sent;
        }
        else
        {
            var errorCode = result.ErrorCode ?? CtsSubmitResult.InvalidResponseCode;
            var errorText = result.ErrorText ?? "CTS did not accept the request.";
            submission.MarkAsCtsFailed(errorCode, errorText);
            outcome = RegistrationBundleOutcome.Failed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await submissionLock.CommitAsync(cancellationToken);

        if (outcome == RegistrationBundleOutcome.Sent)
        {
            LogBundleSentToCts(submissionId, result.ReceiptNumber!.Value);
        }
        else if (result.ErrorCode == CtsSubmitResult.AlreadySubmittedExNum)
        {
            // CTS already has this transaction, but the receipt was never stored (the instance stopped between
            // sending and saving). The receipt cannot be recovered, so the bundle needs reconciling by hand.
            LogBundleNeedsReconciliation(submissionId, request.TxnId);
        }
        else
        {
            LogBundleRejectedByCts(submissionId, result.ErrorCode, result.ErrorText, result.IsTransient);
        }

        return outcome;
    }
}
