// <copyright file="ValidationErrorItem.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.ComponentModel;

/// <summary>
/// Represents a validation error identified while validating a submission animal.
/// </summary>
public class ValidationErrorItem
{
    /// <summary>
    /// Gets or sets the validation error code.
    /// </summary>
    [Description("The validation error code.")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the validation error.
    /// </summary>
    [Description("The description of the validation error.")]
    public string Description { get; set; } = string.Empty;
}
