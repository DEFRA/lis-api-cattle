// <copyright file="HoldingMarkResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>
/// Represents a mark (e.g. herd or flock mark) associated with a holding.
/// </summary>
public class HoldingMarkResponse
{
    /// <summary>
    /// Gets or sets the mark identifier.
    /// </summary>
    [Description("The mark identifier.")]
    public string? Mark { get; set; }

    /// <summary>
    /// Gets or sets the start date from which the mark is valid.
    /// </summary>
    [Description("The start date from which the mark is valid.")]
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date until which the mark is valid.
    /// </summary>
    [Description("The end date until which the mark is valid.")]
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the species associated with the mark.
    /// </summary>
    [Description("The species associated with the mark.")]
    public IReadOnlyList<string> Species { get; set; } = [];
}
