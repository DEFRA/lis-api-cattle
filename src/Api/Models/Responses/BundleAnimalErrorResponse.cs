// <copyright file="BundleAnimalErrorResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model for an animal error within a cattle registration bundle.
/// </summary>
public class BundleAnimalErrorResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the animal error.
    /// </summary>
    [Description("The unique identifier of the animal error.")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the animal.
    /// </summary>
    [Description("The unique identifier of the animal.")]
    public Guid AnimalId { get; set; }

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [Description("The error code.")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error text.
    /// </summary>
    [Description("The error text.")]
    public string ErrorText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the animal error was created.
    /// </summary>
    [Description("The date and time when the animal error was created.")]
    public DateTime? CreatedAt { get; set; }
}
