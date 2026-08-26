// <copyright file="DamRegistrationRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class DamRegistrationRequest
{
    public string? Type { get; set; }

    public string? GeneticDamEarTag { get; set; }

    public string? SurrogateDamEarTag { get; set; }
}
