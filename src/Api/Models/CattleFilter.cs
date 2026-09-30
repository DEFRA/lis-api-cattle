// <copyright file="CattleFilter.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

using Defra.Lis.Api.Models.Responses;

/// <summary>
/// Optional filters for the cattle-on-holding query. All comparisons are case-insensitive;
/// ear tag matching ignores spaces and matches anywhere in the tag; breed matches the breed code or name.
/// </summary>
/// <param name="EarTag">The optional ear tag filter substring.</param>
/// <param name="Breed">The optional breed code or name filter.</param>
/// <param name="Sex">The optional sex filter.</param>
public sealed record CattleFilter(string? EarTag = null, string? Breed = null, string? Sex = null)
{
    /// <summary>
    /// Gets a value indicating whether all filter criteria are empty or whitespace.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(EarTag) && string.IsNullOrWhiteSpace(Breed) && string.IsNullOrWhiteSpace(Sex);

    /// <summary>
    /// Determines whether the specified cattle response matches all configured filter criteria.
    /// </summary>
    /// <param name="cattleResponse">The cattle response to evaluate.</param>
    /// <returns><see langword="true"/> if the cattle response matches the filter; otherwise, <see langword="false"/>.</returns>
    public bool Matches(CattleResponse cattleResponse)
    {
        ArgumentNullException.ThrowIfNull(cattleResponse);

        return MatchesEarTag(cattleResponse) && MatchesBreed(cattleResponse) && MatchesSex(cattleResponse);
    }

    private static string NormaliseEarTag(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private bool MatchesEarTag(CattleResponse cattleResponse)
    {
        if (string.IsNullOrWhiteSpace(EarTag))
        {
            return true;
        }

        return NormaliseEarTag(cattleResponse.EarTag).Contains(NormaliseEarTag(EarTag), StringComparison.Ordinal);
    }

    private bool MatchesBreed(CattleResponse cattleResponse)
    {
        if (string.IsNullOrWhiteSpace(Breed))
        {
            return true;
        }

        var wanted = Breed.Trim();

        return string.Equals(cattleResponse.BreedCode, wanted, StringComparison.OrdinalIgnoreCase)
               || string.Equals(cattleResponse.BreedName, wanted, StringComparison.OrdinalIgnoreCase)
               || string.Equals(cattleResponse.Breed, wanted, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesSex(CattleResponse cattleResponse)
    {
        return string.IsNullOrWhiteSpace(Sex) ||
               string.Equals(cattleResponse.Sex, Sex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
