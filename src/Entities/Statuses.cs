// <copyright file="Statuses.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Entities;

public static class Statuses
{
    public const string Submitted = "submitted";
    public const string Error = "error";
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Complete = "complete";

    /// <summary>A bundle that CTS has finished processing, where at least one animal was rejected (LREG-926).</summary>
    public const string CompleteWithErrors = "complete_with_errors";
}
