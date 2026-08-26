// <copyright file="CtsErrorResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

public class CtsErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorText { get; set; } = string.Empty;
}
