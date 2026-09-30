// <copyright file="BundleResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model for a cattle registration bundle.
/// </summary>
public class BundleResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the bundle.
    /// </summary>
    [Description("The unique identifier of the bundle.")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the client reference for the bundle.
    /// </summary>
    [Description("The client reference for the bundle.")]
    public string ClientReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the county, parish and holding (CPH) number.
    /// </summary>
    [Description("The county, parish and holding (CPH) number.")]
    public string CountyParishHolding { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user or system that submitted the bundle.
    /// </summary>
    [Description("The user or system that submitted the bundle.")]
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the bundle.
    /// </summary>
    [Description("The status of the bundle.")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the bundle was created.
    /// </summary>
    [Description("The date and time when the bundle was created.")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the list of animals in the bundle.
    /// </summary>
    [Description("The list of animals in the bundle.")]
    public List<BundleAnimalResponse> Animals { get; set; } = [];
}
