// <copyright file="SubmissionAnimalError.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public class SubmissionAnimalError
{
    public SubmissionAnimalError(Guid animalId, string errorCode, string errorText)
    {
        if (animalId == Guid.Empty)
        {
            throw new ArgumentException("Animal ID must be valid.", nameof(animalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorText);

        Id = Guid.NewGuid();
        AnimalId = animalId;
        ErrorCode = errorCode;
        ErrorText = errorText;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid AnimalId { get; private set; }

    public SubmissionAnimal Animal { get; private set; } = null!;

    public string ErrorCode { get; private set; } = string.Empty;

    public string ErrorText { get; private set; } = string.Empty;

    public DateTime? CreatedAt { get; private set; }
}
