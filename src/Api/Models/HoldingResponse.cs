// <copyright file="HoldingResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class HoldingResponse
{
    public string Cph { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? HoldingType { get; set; }

    public IReadOnlyList<string> Address { get; set; } = [];

    public string? KeeperName { get; set; }

    public IReadOnlyList<string> HerdMarks { get; set; } = [];

    public IReadOnlyList<string> AllowedSpecies { get; set; } = [];
}
