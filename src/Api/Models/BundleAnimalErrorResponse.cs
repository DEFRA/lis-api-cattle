// <copyright file="BundleAnimalErrorResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class BundleAnimalErrorResponse
{
    public Guid Id { get; set; }

    public Guid AnimalId { get; set; }

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorText { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
}
