// <copyright file="SubmissionAnimal.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class SubmissionAnimal
{
    private readonly List<SubmissionAnimalError> errors = [];

    // Sonar S107: one parameter per animal column, matching Submission.AddAnimal, which calls it.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "One parameter per animal column, as Submission.AddAnimal passes them.")]
    public SubmissionAnimal(
        Guid submissionId,
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
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException("Submission ID must be valid.", nameof(submissionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = Guid.NewGuid();
        SubmissionId = submissionId;
        EarTag = earTag ?? string.Empty;
        Status = status;
        DateBirth = dateBirth;
        Sex = sex;
        Breed = breed;
        DamType = damType;
        DamGeneticEarTag = damGeneticEarTag;
        DamSurrogateEarTag = damSurrogateEarTag;
        SireEarTag = sireEarTag;
        SireName = sireName;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public Submission Submission { get; private set; } = null!;

    public string Status { get; private set; } = string.Empty;

    public string EarTag { get; private set; } = string.Empty;

    public DateOnly? DateBirth { get; private set; }

    public string? Sex { get; private set; }

    public string? Breed { get; private set; }

    public string? DamType { get; private set; }

    public string? DamGeneticEarTag { get; private set; }

    public string? DamSurrogateEarTag { get; private set; }

    public string? SireEarTag { get; private set; }

    public string? SireName { get; private set; }

    public IReadOnlyCollection<SubmissionAnimalError> Errors => errors.AsReadOnly();

    /// <summary>Gets a value indicating whether CTS has given this animal its final outcome (accepted or rejected).</summary>
    public bool HasFinalCtsOutcome => Status == Statuses.Complete || Status == Statuses.Error;

    public SubmissionAnimalError AddError(string errorCode, string errorText)
    {
        var error = new SubmissionAnimalError(Id, errorCode, errorText);
        errors.Add(error);
        return error;
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
        errors.Clear();
    }

    public void MarkAsError(string errorCode, string errorText)
    {
        Status = Statuses.Error;
        AddError(errorCode, errorText);
    }

    public void ClearErrors()
    {
        errors.Clear();
    }

    /// <summary>
    /// Records that CTS accepted the animal. An animal that already has its final outcome is left as it is, so
    /// polling the same CTS results again changes nothing.
    /// </summary>
    /// <returns><see langword="true"/> if the animal changed.</returns>
    public bool RecordCtsAccepted()
    {
        if (HasFinalCtsOutcome)
        {
            return false;
        }

        Status = Statuses.Complete;
        return true;
    }

    /// <summary>
    /// Records that CTS rejected the animal, with one error row per distinct cause. An animal that already has its
    /// final outcome is left as it is, so polling the same CTS results again adds no duplicate error rows.
    /// </summary>
    /// <param name="causes">The CTS causes (error code and text).</param>
    /// <returns><see langword="true"/> if the animal changed.</returns>
    public bool RecordCtsRejected(IEnumerable<(string ErrorCode, string ErrorText)> causes)
    {
        ArgumentNullException.ThrowIfNull(causes);

        if (HasFinalCtsOutcome)
        {
            return false;
        }

        var distinct = causes
            .Where(c => !string.IsNullOrWhiteSpace(c.ErrorCode))
            .Select(c => (c.ErrorCode, ErrorText: string.IsNullOrWhiteSpace(c.ErrorText) ? c.ErrorCode : c.ErrorText))
            .Distinct()
            .ToList();

        if (distinct.Count == 0)
        {
            // CTS rejected the row without saying why; the failure must still be visible on the animal.
            distinct.Add(("CTS_REJECTED", "CTS rejected the animal without giving a cause."));
        }

        Status = Statuses.Error;
        foreach (var (errorCode, errorText) in distinct)
        {
            AddError(errorCode, errorText);
        }

        return true;
    }
}
