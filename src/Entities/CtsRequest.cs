// <copyright file="CtsRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class CtsRequest
{
    public const int MaxTxnIdLength = 40;

    public CtsRequest(Guid submissionId, string txnId, string operation, string payload)
    {
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException("Submission ID must be valid.", nameof(submissionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(txnId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (txnId.Length > MaxTxnIdLength)
        {
            throw new ArgumentException($"A CTS transaction ID cannot be longer than {MaxTxnIdLength} characters.", nameof(txnId));
        }

        Id = Guid.NewGuid();
        SubmissionId = submissionId;
        TxnId = txnId;
        Operation = operation;
        Payload = payload;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public Submission Submission { get; private set; } = null!;

    public string TxnId { get; private set; }

    public string Operation { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public long? ReceiptNumber { get; private set; }

    public string? LastError { get; private set; }

    internal void RecordReceipt(long receiptNumber, DateTimeOffset sentAt)
    {
        ReceiptNumber = receiptNumber;
        SentAt = sentAt;
        LastError = null;
    }

    internal void RecordFailure(string error)
    {
        LastError = error;
    }
}
