// <copyright file="Animal.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

/// <summary>
/// The LIS master record for one animal, keyed by its ear tag (LREG-1003). It is created in the
/// <see cref="AnimalStatuses.Pending"/> state when the registration bundle that names the animal is received, and it
/// keeps a link to the submission and submission animal that created it. Errors CTS reports for the animal are
/// recorded against it.
/// </summary>
public class Animal
{
    /// <summary>The error source for errors CTS reported.</summary>
    public const string CtsErrorSource = "cts";

    private readonly List<AnimalError> errors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Animal"/> class, in the <see cref="AnimalStatuses.Pending"/>
    /// state, with the details received for the submission animal.
    /// </summary>
    /// <param name="submissionAnimal">The submission animal that names this animal.</param>
    /// <param name="earTag">The animal's normalised ear tag, which identifies it across every submission.</param>
    public Animal(SubmissionAnimal submissionAnimal, string earTag)
    {
        ArgumentNullException.ThrowIfNull(submissionAnimal);
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        Id = Guid.NewGuid();
        EarTag = earTag;
        Status = AnimalStatuses.Pending;
        DateBirth = submissionAnimal.DateBirth;
        Sex = submissionAnimal.Sex;
        Breed = submissionAnimal.Breed;
        DamType = submissionAnimal.DamType;
        DamGeneticEarTag = submissionAnimal.DamGeneticEarTag;
        DamSurrogateEarTag = submissionAnimal.DamSurrogateEarTag;
        SireEarTag = submissionAnimal.SireEarTag;
        SireName = submissionAnimal.SireName;
        SubmissionId = submissionAnimal.SubmissionId;
        SubmissionAnimalId = submissionAnimal.Id;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private Animal()
    {
    }

    public Guid Id { get; private set; }

    public string EarTag { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public DateOnly? DateBirth { get; private set; }

    public string? Sex { get; private set; }

    public string? Breed { get; private set; }

    public string? DamType { get; private set; }

    public string? DamGeneticEarTag { get; private set; }

    public string? DamSurrogateEarTag { get; private set; }

    public string? SireEarTag { get; private set; }

    public string? SireName { get; private set; }

    /// <summary>Gets the submission that created this record.</summary>
    public Guid SubmissionId { get; private set; }

    /// <summary>Gets the submission animal that created this record.</summary>
    public Guid SubmissionAnimalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<AnimalError> Errors => errors.AsReadOnly();

    /// <summary>
    /// Records that CTS accepted the animal: a pending animal becomes <see cref="AnimalStatuses.Registered"/>.
    /// An animal that already has its outcome is left as it is.
    /// </summary>
    /// <param name="at">When the outcome was recorded.</param>
    /// <returns><see langword="true"/> if the animal changed.</returns>
    public bool RecordCtsAccepted(DateTimeOffset at)
    {
        if (Status != AnimalStatuses.Pending)
        {
            return false;
        }

        Status = AnimalStatuses.Registered;
        UpdatedAt = at;
        return true;
    }

    /// <summary>
    /// Records that CTS rejected the animal: a pending animal moves to <see cref="AnimalStatuses.Error"/>, and each
    /// error is stored once per code, so recording the same CTS response again adds no rows.
    /// </summary>
    /// <param name="causes">The errors, as CTWS code and text.</param>
    /// <param name="at">When the errors were recorded.</param>
    /// <returns><see langword="true"/> if the animal changed.</returns>
    public bool RecordCtsRejected(IEnumerable<(string ErrorCode, string ErrorText)> causes, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(causes);

        var changed = false;
        if (Status == AnimalStatuses.Pending)
        {
            Status = AnimalStatuses.Error;
            changed = true;
        }

        foreach (var (errorCode, errorText) in causes)
        {
            if (string.IsNullOrWhiteSpace(errorCode)
                || errors.Exists(e => string.Equals(e.ErrorCode, errorCode, StringComparison.Ordinal)))
            {
                continue;
            }

            errors.Add(new AnimalError(Id, errorCode, string.IsNullOrWhiteSpace(errorText) ? errorCode : errorText, CtsErrorSource, at));
            changed = true;
        }

        if (changed)
        {
            UpdatedAt = at;
        }

        return changed;
    }
}
