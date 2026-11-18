// <copyright file="Submission.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class Submission
{
    private readonly List<SubmissionAnimal> animals = [];

    public Submission(string clientReference, string countyParishHolding, string submittedBy, string status = Statuses.Submitted)
        : this(Guid.NewGuid(), clientReference, countyParishHolding, submittedBy, status)
    {
    }

    public Submission(Guid id, string clientReference, string countyParishHolding, string submittedBy, string status = Statuses.Submitted)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Submission ID must be valid.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(countyParishHolding);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = id;
        ClientReference = clientReference;
        CountyParishHolding = countyParishHolding;
        SubmittedBy = submittedBy;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string ClientReference { get; private set; }

    public string CountyParishHolding { get; private set; }

    public string SubmittedBy { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? CorrelationId { get; private set; }

    public long? CtsReceiptNumber { get; private set; }

    public DateTimeOffset? CtsSentAt { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorText { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CtsLastPolledAt { get; private set; }

    public CtsRequest? CtsRequest { get; private set; }

    public bool HasCtsOutcome => CtsReceiptNumber.HasValue || Status == Statuses.Error;

    public IReadOnlyCollection<SubmissionAnimal> Animals => animals.AsReadOnly();

    // Sonar S107: one parameter per animal column, matching the SubmissionAnimal constructor it calls.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "One parameter per animal column, as the SubmissionAnimal constructor takes.")]
    public SubmissionAnimal AddAnimal(
        string earTag,
        string status = Statuses.Submitted,
        DateOnly? dateBirth = null,
        string? sex = null,
        string? breed = null,
        string? damType = null,
        string? damGeneticEarTag = null,
        string? damSurrogateEarTag = null,
        string? sireEarTag = null,
        string? sireName = null)
    {
        var animal = new SubmissionAnimal(
            Id,
            earTag,
            status,
            dateBirth,
            sex,
            breed,
            damType,
            damGeneticEarTag,
            damSurrogateEarTag,
            sireEarTag,
            sireName);

        animals.Add(animal);
        return animal;
    }

    public void UpdateStatus(string newStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        Status = newStatus;
    }

    public void MarkAsProcessing()
    {
        Status = Statuses.Processing;
    }

    public void MarkAsComplete()
    {
        Status = Statuses.Complete;
    }

    public void MarkAsError()
    {
        Status = Statuses.Error;
    }

    public void RecordCorrelationId(string? correlationId)
    {
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
    }

    public CtsRequest RecordCtsRequest(string txnId, string operation, string payload)
    {
        if (CtsRequest != null)
        {
            throw new InvalidOperationException($"Submission {Id} already has a CTS request.");
        }

        CtsRequest = new CtsRequest(Id, txnId, operation, payload);
        UpdatedAt = DateTimeOffset.UtcNow;
        return CtsRequest;
    }

    public void MarkAsSentToCts(long receiptNumber, DateTimeOffset sentAt)
    {
        if (CtsRequest == null)
        {
            throw new InvalidOperationException($"Submission {Id} cannot be marked as sent without a stored CTS request.");
        }

        CtsRequest.RecordReceipt(receiptNumber, sentAt);
        CtsReceiptNumber = receiptNumber;
        CtsSentAt = sentAt;
        ErrorCode = null;
        ErrorText = null;
        Status = Statuses.Submitted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsCtsFailed(string errorCode, string errorText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorText);

        // A bundle that CTS did not accept must never look as though it was sent.
        CtsRequest?.RecordFailure($"{errorCode}: {errorText}");
        CtsReceiptNumber = null;
        CtsSentAt = null;
        ErrorCode = errorCode;
        ErrorText = errorText;
        Status = Statuses.Error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Records when CTS was last asked for this bundle's results, so the poller can take the longest-waiting bundles first.</summary>
    /// <param name="polledAt">When CTS was asked.</param>
    public void RecordCtsPoll(DateTimeOffset polledAt)
    {
        CtsLastPolledAt = polledAt;
    }

    /// <summary>
    /// Completes the bundle once every animal has its final CTS outcome: <see cref="Statuses.Complete"/> when CTS
    /// accepted them all, otherwise <see cref="Statuses.CompleteWithErrors"/>, with the failures on the animals.
    /// </summary>
    /// <param name="completedAt">When the bundle completed.</param>
    /// <returns><see langword="true"/> if the bundle was completed by this call.</returns>
    public bool TryCompleteFromCtsResults(DateTimeOffset completedAt)
    {
        if (CompletedAt.HasValue || animals.Count == 0 || !animals.All(a => a.HasFinalCtsOutcome))
        {
            return false;
        }

        Status = animals.Any(a => a.Status == Statuses.Error) ? Statuses.CompleteWithErrors : Statuses.Complete;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
        return true;
    }

    /// <summary>
    /// Marks a sent bundle as errored because its CTS results can never be fetched (for example CTS does not know
    /// the receipt). The receipt is kept for reconciliation; the bundle is not polled again.
    /// </summary>
    /// <param name="errorCode">The CTS error code.</param>
    /// <param name="errorText">The CTS error text.</param>
    /// <param name="failedAt">When the bundle was marked as errored.</param>
    public void MarkAsCtsResultsFailed(string errorCode, string errorText, DateTimeOffset failedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorText);

        ErrorCode = errorCode;
        ErrorText = errorText;
        Status = Statuses.Error;
        UpdatedAt = failedAt;
    }

    public void RefreshStatusFromAnimals()
    {
        if (animals.Count == 0)
        {
            return;
        }

        if (animals.Any(a => a.Status == Statuses.Error))
        {
            Status = Statuses.Error;
        }
        else if (animals.Any(a => a.Status == Statuses.Processing || a.Status == Statuses.Submitted || a.Status == Statuses.Pending))
        {
            Status = Statuses.Processing;
        }
        else if (animals.All(a => a.Status == Statuses.Complete))
        {
            Status = Statuses.Complete;
        }
    }
}
