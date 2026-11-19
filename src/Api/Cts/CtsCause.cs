// <copyright file="CtsCause.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>One cause CTS gave for rejecting a row.</summary>
/// <param name="Code">The CTS cause code, for example <c>CTWS014</c>.</param>
/// <param name="Description">The cause description.</param>
/// <param name="Severity">The cause severity (<c>e</c> or <c>w</c>).</param>
/// <param name="Field">The request attribute the cause applies to.</param>
public sealed record CtsCause(string Code, string Description, string? Severity, string? Field);
