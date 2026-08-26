// <copyright file="AnimalRegistrationRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class AnimalRegistrationRequest
{
    public string EarTag { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string? Sex { get; set; }

    public string? Breed { get; set; }

    public DamRegistrationRequest? Dam { get; set; }

    public SireRegistrationRequest? Sire { get; set; }
}
