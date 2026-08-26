// <copyright file="ValidationErrorItem.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public class ValidationErrorItem
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
