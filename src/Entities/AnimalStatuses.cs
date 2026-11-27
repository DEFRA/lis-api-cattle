// <copyright file="AnimalStatuses.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

/// <summary>The states of an animal record (LREG-1003).</summary>
public static class AnimalStatuses
{
    /// <summary>The registration was received and CTS has not given its outcome yet.</summary>
    public const string Pending = "pending";

    /// <summary>CTS accepted the registration.</summary>
    public const string Registered = "registered";

    /// <summary>CTS rejected the registration; the errors are recorded against the animal.</summary>
    public const string Error = "error";
}
