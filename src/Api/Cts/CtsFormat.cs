// <copyright file="CtsFormat.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

using System.Text.RegularExpressions;

/// <summary>
/// Converts registration values to the formats the CTSWeb schemas (ctws_types-V1-0.xsd) require.
/// </summary>
public static partial class CtsFormat
{
    /// <summary>The longest ear tag CTS accepts (Eartag_Type).</summary>
    public const int MaxEarTagLength = 14;

    /// <summary>The longest breed code CTS accepts (BreedCode_Type).</summary>
    public const int MaxBreedCodeLength = 5;

    /// <summary>
    /// Converts an ear tag to the form CTS holds it in. A current-format tag (two letters and twelve digits, which
    /// people often write as <c>UK 12 3456 100003</c>) is sent without spaces. Any other tag, such as the older
    /// herd-mark form <c>UK A1213 00003</c>, keeps its inner spaces, because CTS treats <c>UKAA2233 00012</c> and
    /// <c>UKAA223300012</c> as different tags. In every case the ends are trimmed, runs of whitespace become one
    /// space, and letters are upper-cased.
    /// </summary>
    /// <param name="earTag">The ear tag as entered.</param>
    /// <returns>The ear tag in CTS form, or <see langword="null"/> when none was supplied.</returns>
    public static string? EarTag(string? earTag)
    {
        if (string.IsNullOrWhiteSpace(earTag))
        {
            return null;
        }

        var tag = Whitespace().Replace(earTag.Trim(), " ").ToUpperInvariant();
        var compact = tag.Replace(" ", string.Empty, StringComparison.Ordinal);
        return CurrentFormatEarTag().IsMatch(compact) ? compact : tag;
    }

    /// <summary>
    /// Maps the sex values the registration journey uses to the CTS values <c>m</c> and <c>f</c>.
    /// </summary>
    /// <param name="sex">The sex as supplied (m, f, male or female, in any case).</param>
    /// <returns>The CTS value, or <see langword="null"/> when the value is not recognised.</returns>
    public static string? Sex(string? sex) => sex?.Trim().ToUpperInvariant() switch
    {
        "M" or "MALE" => "m",
        "F" or "FEMALE" => "f",
        _ => null,
    };

    /// <summary>Checks a CTS breed code: one to five capital letters.</summary>
    /// <param name="breed">The breed value as supplied.</param>
    /// <returns><see langword="true"/> when CTS accepts it as a breed code.</returns>
    public static bool IsBreedCode(string? breed) => breed != null && BreedCode().IsMatch(breed);

    /// <summary>Checks a county/parish/holding number in the CTS form <c>nn/nnn/nnnn</c>.</summary>
    /// <param name="cph">The CPH as supplied.</param>
    /// <returns><see langword="true"/> when CTS accepts it as a location code.</returns>
    public static bool IsCph(string? cph) => cph != null && Cph().IsMatch(cph);

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^[A-Z]{2}\d{12}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CurrentFormatEarTag();

    [GeneratedRegex("^[A-Z]{1,5}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BreedCode();

    [GeneratedRegex(@"^\d{2}/\d{3}/\d{4}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Cph();
}
