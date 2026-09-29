// <copyright file="NullGuardExtensions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace System;

using System.Runtime.CompilerServices;

public static class NullGuardExtensions
{
    public static T ThrowIfNull<T>(
        this T? argument,
        [CallerArgumentExpression("argument")] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
        return argument;
    }
}
