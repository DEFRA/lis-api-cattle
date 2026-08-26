// <copyright file="SubmissionAnimal.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class SubmissionAnimal
{
    private readonly List<SubmissionAnimalError> errors = [];

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
}
