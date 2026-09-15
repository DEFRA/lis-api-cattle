// <copyright file="SubmissionValidationContext.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.Text.RegularExpressions;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;

/// <summary>
/// Everything the per-animal rules need that is computed once per submission:
/// the configured thresholds and patterns, the CADS animals for the holding, the
/// animals already held in other submissions, and the ear tags duplicated in this file.
/// </summary>
internal sealed class SubmissionValidationContext
{
    public SubmissionValidationContext(
        Submission submission,
        SubmissionValidationOptions options,
        IReadOnlyList<CattleResponse> cadsCattle,
        IReadOnlyList<SubmissionAnimal> existingAnimals,
        DateOnly today)
    {
        Submission = submission;
        Options = options;
        CadsCattle = cadsCattle;
        ExistingAnimals = existingAnimals;
        Today = today;
        EarTagRegex = new Regex(options.EarTagRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        var cphRegex = new Regex(options.CphRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        IsCphValid = !string.IsNullOrWhiteSpace(submission.CountyParishHolding) && cphRegex.IsMatch(submission.CountyParishHolding);

        DuplicateEarTags = submission.Animals
            .Where(a => !string.IsNullOrWhiteSpace(a.EarTag))
            .GroupBy(a => a.EarTag.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Submission Submission { get; }

    public SubmissionValidationOptions Options { get; }

    public IReadOnlyList<CattleResponse> CadsCattle { get; }

    public IReadOnlyList<SubmissionAnimal> ExistingAnimals { get; }

    public DateOnly Today { get; }

    public Regex EarTagRegex { get; }

    public bool IsCphValid { get; }

    public HashSet<string> DuplicateEarTags { get; }

    public bool IsValidEarTagFormat(string earTag) => EarTagRegex.IsMatch(earTag);

    /// <summary>
    /// Finds an animal by ear tag in CADS first, then in animals held by other submissions.
    /// </summary>
    public CattleResponse? FindAnimal(string earTag)
    {
        var cadsRecord = CadsCattle.FirstOrDefault(c => EarTags.AreEqual(c.EarTag, earTag));
        if (cadsRecord != null)
        {
            return cadsRecord;
        }

        return ExistingAnimals
            .Where(a => EarTags.AreEqual(a.EarTag, earTag))
            .Select(a => new CattleResponse { EarTag = a.EarTag, DateBirth = a.DateBirth, Sex = a.Sex })
            .FirstOrDefault();
    }

    public bool IsEarTagAlreadyUsed(string earTag)
    {
        return CadsCattle.Any(c => EarTags.AreEqual(c.EarTag, earTag))
               || ExistingAnimals.Any(a => EarTags.AreEqual(a.EarTag, earTag) && a.Status == Statuses.Complete);
    }

    /// <summary>
    /// Birth dates of every other calf (in this submission or already held) recorded against the dam.
    /// </summary>
    public IEnumerable<DateOnly> OtherCalfBirthDates(SubmissionAnimal calf, string damEarTag)
    {
        var siblingsInFile = Submission.Animals.Where(a => a.Id != calf.Id);

        return siblingsInFile
            .Concat(ExistingAnimals)
            .Where(a => a.DateBirth.HasValue && EarTags.HasDam(a, damEarTag))
            .Select(a => a.DateBirth!.Value);
    }
}
