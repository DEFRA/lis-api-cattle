// <copyright file="AnimalError.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

/// <summary>
/// An error recorded against an animal record, with the same CTWS code and text the API's submission validation
/// uses (LREG-1003).
/// </summary>
public class AnimalError
{
    public AnimalError(Guid animalId, string errorCode, string errorText, string source, DateTimeOffset createdAt)
    {
        if (animalId == Guid.Empty)
        {
            throw new ArgumentException("Animal ID must be valid.", nameof(animalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorText);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Id = Guid.NewGuid();
        AnimalId = animalId;
        ErrorCode = errorCode;
        ErrorText = errorText;
        Source = source;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid AnimalId { get; private set; }

    public string ErrorCode { get; private set; }

    public string ErrorText { get; private set; }

    /// <summary>Gets where the error came from: <see cref="Animal.CtsErrorSource"/> for errors CTS reported.</summary>
    public string Source { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
