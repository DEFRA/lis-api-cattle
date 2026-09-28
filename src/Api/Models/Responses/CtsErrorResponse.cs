// <copyright file="CtsErrorResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model for a Cattle Tracing System (CTS) error.
/// </summary>
public class CtsErrorResponse
{
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
}
