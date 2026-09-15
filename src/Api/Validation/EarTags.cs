// <copyright file="EarTags.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using Defra.Lis.Entities;

/// <summary>
/// Ear tag comparison helpers: tags are compared trimmed and case-insensitively.
/// </summary>
internal static class EarTags
{
    public static bool AreEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string? Normalise(string? earTag) =>
        string.IsNullOrWhiteSpace(earTag) ? null : earTag.Trim();

    /// <summary>
    /// The dam recorded for a calf: the genetic dam when given, otherwise the surrogate dam.
    /// </summary>
    public static string? DamOf(SubmissionAnimal animal) =>
        Normalise(animal.DamGeneticEarTag) ?? Normalise(animal.DamSurrogateEarTag);

    public static bool HasDam(SubmissionAnimal animal, string damEarTag) =>
        AreEqual(animal.DamGeneticEarTag, damEarTag) || AreEqual(animal.DamSurrogateEarTag, damEarTag);

    public static bool IsFemale(string? sex) =>
        string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase) || string.Equals(sex, "Female", StringComparison.OrdinalIgnoreCase);

    public static bool IsMale(string? sex) =>
        string.Equals(sex, "M", StringComparison.OrdinalIgnoreCase) || string.Equals(sex, "Male", StringComparison.OrdinalIgnoreCase);
}
