// <copyright file="BundleResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class BundleResponse
{
    public Guid Id { get; set; }

    public string ClientReference { get; set; } = string.Empty;

    public string CountyParishHolding { get; set; } = string.Empty;

    public string SubmittedBy { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<BundleAnimalResponse> Animals { get; set; } = [];
}
