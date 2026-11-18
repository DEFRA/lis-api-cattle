// <copyright file="CtsRejectedBirth.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>A row CTS rejected.</summary>
/// <param name="RowNum">The row number from the Birth request.</param>
/// <param name="EarTag">The ear tag CTS echoed back, if any.</param>
/// <param name="Causes">Why CTS rejected the row.</param>
public sealed record CtsRejectedBirth(int RowNum, string? EarTag, IReadOnlyList<CtsCause> Causes);
