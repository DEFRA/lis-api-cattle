// <copyright file="SubmissionAnimalRules.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using Defra.Lis.Entities;

/// <summary>
/// The CTWS registration rules applied to each animal in a submission. Each rule group is a
/// small method so the checks stay readable and individually testable.
/// </summary>
internal static class SubmissionAnimalRules
{
    public static List<ValidationErrorItem> Validate(SubmissionAnimal animal, SubmissionValidationContext context)
    {
        var errors = new List<ValidationErrorItem>();

        CheckLocation(context, errors);
        CheckEarTag(animal, context, errors);
        CheckBreed(animal, errors);
        CheckBirthDate(animal, context, errors);
        CheckDamTags(animal, errors);
        CheckSire(animal, context, errors);
        CheckDam(animal, context, errors);

        return errors;
    }

    private static ValidationErrorItem Error(string code) => new()
    {
        Code = code,
        Description = ValidationRuleCodes.GetDescription(code),
    };

    // CTWS079: the submission's CPH must be a valid location.
    private static void CheckLocation(SubmissionValidationContext context, List<ValidationErrorItem> errors)
    {
        if (!context.IsCphValid)
        {
            errors.Add(Error(ValidationRuleCodes.Ctws079));
        }
    }

    // CTWS003 missing, CTWS004 invalid format, CTWS204 duplicated in file, CTWS192 already used.
    private static void CheckEarTag(SubmissionAnimal animal, SubmissionValidationContext context, List<ValidationErrorItem> errors)
    {
        var earTag = EarTags.Normalise(animal.EarTag);

        if (earTag is null)
        {
            errors.Add(Error(ValidationRuleCodes.Ctws003));
            return;
        }

        if (!context.IsValidEarTagFormat(earTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws004));
        }

        if (context.DuplicateEarTags.Contains(earTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws204));
        }

        if (context.IsEarTagAlreadyUsed(earTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws192));
        }
    }

    // CTWS014: a breed, when supplied, cannot be blank.
    private static void CheckBreed(SubmissionAnimal animal, List<ValidationErrorItem> errors)
    {
        if (animal.Breed != null && string.IsNullOrWhiteSpace(animal.Breed))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws014));
        }
    }

    // CTWS023 birth date in the future, otherwise CTWS203 application later than allowed.
    private static void CheckBirthDate(SubmissionAnimal animal, SubmissionValidationContext context, List<ValidationErrorItem> errors)
    {
        if (!animal.DateBirth.HasValue)
        {
            return;
        }

        var birthDate = animal.DateBirth.Value;

        if (birthDate > context.Today)
        {
            errors.Add(Error(ValidationRuleCodes.Ctws023));
            return;
        }

        if (context.Today.DayNumber - birthDate.DayNumber > context.Options.MaxApplicationLateDays)
        {
            errors.Add(Error(ValidationRuleCodes.Ctws203));
        }
    }

    // CTWS034 genetic dam = animal, CTWS042 surrogate dam = animal, CTWS043 surrogate dam = genetic dam.
    private static void CheckDamTags(SubmissionAnimal animal, List<ValidationErrorItem> errors)
    {
        if (EarTags.AreEqual(animal.DamGeneticEarTag, animal.EarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws034));
        }

        if (EarTags.AreEqual(animal.DamSurrogateEarTag, animal.EarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws042));
        }

        if (EarTags.AreEqual(animal.DamSurrogateEarTag, animal.DamGeneticEarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws043));
        }
    }

    // CTWS044 invalid sire tag, CTWS050/051/052 sire equals animal, genetic dam or surrogate dam, CTWS196 sire is female.
    private static void CheckSire(SubmissionAnimal animal, SubmissionValidationContext context, List<ValidationErrorItem> errors)
    {
        var sireTag = EarTags.Normalise(animal.SireEarTag);

        if (sireTag is null)
        {
            return;
        }

        if (!context.IsValidEarTagFormat(sireTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws044));
        }

        if (EarTags.AreEqual(sireTag, animal.EarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws050));
        }

        if (EarTags.AreEqual(sireTag, animal.DamGeneticEarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws051));
        }

        if (EarTags.AreEqual(sireTag, animal.DamSurrogateEarTag))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws052));
        }

        if (EarTags.IsFemale(context.FindAnimal(sireTag)?.Sex))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws196));
        }
    }

    // CTWS195 dam is male, CTWS202 dam too young or too old at calving, CTWS200 dam calved within the calving interval.
    private static void CheckDam(SubmissionAnimal animal, SubmissionValidationContext context, List<ValidationErrorItem> errors)
    {
        var damEarTag = EarTags.DamOf(animal);

        if (damEarTag is null)
        {
            return;
        }

        var damRecord = context.FindAnimal(damEarTag);

        if (EarTags.IsMale(damRecord?.Sex))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws195));
        }

        if (!animal.DateBirth.HasValue)
        {
            return;
        }

        var calfBirth = animal.DateBirth.Value;

        if (damRecord?.DateBirth is { } damBirth && IsDamAgeOutOfRange(damBirth, calfBirth, context.Options))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws202));
        }

        if (HasCalvedWithinInterval(calfBirth, context.OtherCalfBirthDates(animal, damEarTag), context.Options.MinCalvingIntervalDays))
        {
            errors.Add(Error(ValidationRuleCodes.Ctws200));
        }
    }

    private static bool IsDamAgeOutOfRange(DateOnly damBirth, DateOnly calfBirth, Configurations.SubmissionValidationOptions options)
    {
        var ageInMonths = CompletedMonthsBetween(damBirth, calfBirth);
        var ageInYears = CompletedYearsBetween(damBirth, calfBirth);

        return ageInMonths < options.MinDamAgeInMonths || ageInYears > options.MaxDamAgeInYears;
    }

    private static int CompletedMonthsBetween(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + (to.Month - from.Month);
        return to.Day < from.Day ? months - 1 : months;
    }

    private static int CompletedYearsBetween(DateOnly from, DateOnly to)
    {
        var years = to.Year - from.Year;
        var beforeAnniversary = to.Month < from.Month || (to.Month == from.Month && to.Day < from.Day);
        return beforeAnniversary ? years - 1 : years;
    }

    // Same-day births (twins or multiples) are allowed; anything else inside the interval is not.
    private static bool HasCalvedWithinInterval(DateOnly calfBirth, IEnumerable<DateOnly> otherCalfBirths, int minCalvingIntervalDays)
    {
        return otherCalfBirths
            .Select(other => Math.Abs(calfBirth.DayNumber - other.DayNumber))
            .Any(diffDays => diffDays > 0 && diffDays < minCalvingIntervalDays);
    }
}
