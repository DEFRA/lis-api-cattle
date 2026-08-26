// <copyright file="RegistrationBundleRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class RegistrationBundleRequest
{
    public string ClientReference { get; set; } = string.Empty;

    public HoldingRequest? Holding { get; set; }

    public string? SubmittedBy { get; set; }

    public List<AnimalRegistrationRequest> Animals { get; set; } = [];
}
