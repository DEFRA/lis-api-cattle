// <copyright file="Submission.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class Submission
{
    private readonly List<SubmissionAnimal> animals = [];

    public Submission(string clientReference, string countyParishHolding, string submittedBy, string status = Statuses.Submitted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(countyParishHolding);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = Guid.NewGuid();
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

    public IReadOnlyCollection<SubmissionAnimal> Animals => animals.AsReadOnly();

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
