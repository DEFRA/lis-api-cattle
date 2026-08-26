// <copyright file="CtsAnimalStatusResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

using Defra.Lis.Entities;

public class CtsAnimalStatusResponse
{
    public string EarTag { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsClean => string.Equals(Status, "clean", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(Status, Statuses.Complete, StringComparison.OrdinalIgnoreCase);

    public bool IsError => string.Equals(Status, Statuses.Error, StringComparison.OrdinalIgnoreCase);

    public List<CtsErrorResponse> Errors { get; set; } = [];
}
