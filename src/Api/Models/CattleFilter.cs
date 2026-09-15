// <copyright file="CattleFilter.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

/// <summary>
/// Optional filters for the cattle-on-holding query. All comparisons are case-insensitive;
/// ear tag matching ignores spaces and matches anywhere in the tag; breed matches the breed code or name.
/// </summary>
public sealed record CattleFilter(string? EarTag = null, string? Breed = null, string? Sex = null)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(EarTag) && string.IsNullOrWhiteSpace(Breed) && string.IsNullOrWhiteSpace(Sex);

    public bool Matches(CattleResponse cattle)
    {
        ArgumentNullException.ThrowIfNull(cattle);

        return MatchesEarTag(cattle) && MatchesBreed(cattle) && MatchesSex(cattle);
    }

    private static string NormaliseEarTag(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private bool MatchesEarTag(CattleResponse cattle)
    {
        if (string.IsNullOrWhiteSpace(EarTag))
        {
            return true;
        }

        return NormaliseEarTag(cattle.EarTag).Contains(NormaliseEarTag(EarTag), StringComparison.Ordinal);
    }

    private bool MatchesBreed(CattleResponse cattle)
    {
        if (string.IsNullOrWhiteSpace(Breed))
        {
            return true;
        }

        var wanted = Breed.Trim();

        return string.Equals(cattle.BreedCode, wanted, StringComparison.OrdinalIgnoreCase)
               || string.Equals(cattle.BreedName, wanted, StringComparison.OrdinalIgnoreCase)
               || string.Equals(cattle.Breed, wanted, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesSex(CattleResponse cattle)
    {
        if (string.IsNullOrWhiteSpace(Sex))
        {
            return true;
        }

        return string.Equals(cattle.Sex, Sex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
