// <copyright file="SubmissionValidationService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.Text.RegularExpressions;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SubmissionValidationService(
    DbContext dbContext,
    ICadsService cadsService,
    IOptions<SubmissionValidationOptions>? options = null,
    ILogger<SubmissionValidationService>? logger = null)
    : ISubmissionValidationService
{
    private readonly DbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly ICadsService cadsService = cadsService ?? throw new ArgumentNullException(nameof(cadsService));
    private readonly SubmissionValidationOptions options = options?.Value ?? new SubmissionValidationOptions();

    public SubmissionValidationService(
        PostgresDbContext dbContext,
        ICadsService cadsService,
        IOptions<SubmissionValidationOptions>? options = null,
        ILogger<SubmissionValidationService>? logger = null)
        : this((DbContext)dbContext, cadsService, options, logger)
    {
    }

    public async Task<SubmissionValidationResult> ValidateSubmissionByIdAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await dbContext.Set<Submission>()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

        if (submission == null)
        {
            logger?.LogWarning("Submission with ID {SubmissionId} not found for validation", submissionId);
            throw new KeyNotFoundException($"Submission with ID {submissionId} not found.");
        }

        var result = await ValidateSubmissionAsync(submission, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<SubmissionValidationResult> ValidateSubmissionAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        logger?.LogInformation("Starting validation for submission {SubmissionId} (CPH: {Cph})", submission.Id, submission.CountyParishHolding);

        var result = new SubmissionValidationResult
        {
            SubmissionId = submission.Id,
            IsValid = true,
        };

        var earTagRegex = new Regex(options.EarTagRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        var cphRegex = new Regex(options.CphRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        // Location / CPH format validation
        bool isCphValid = !string.IsNullOrWhiteSpace(submission.CountyParishHolding) && cphRegex.IsMatch(submission.CountyParishHolding);

        // Fetch CADS cattle for this CPH and holding history
        IEnumerable<CattleResponse> cadsCattle = [];
        try
        {
            if (!string.IsNullOrWhiteSpace(submission.CountyParishHolding))
            {
                cadsCattle = await cadsService.GetCattleByCphAsync(submission.CountyParishHolding);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not fetch CADS cattle for holding {Cph}", submission.CountyParishHolding);
        }

        var cadsCattleList = cadsCattle.ToList();

        // Check for duplicate ear tags in the submission file/bundle (CTWS204)
        var earTagGroups = submission.Animals
            .Where(a => !string.IsNullOrWhiteSpace(a.EarTag))
            .GroupBy(a => a.EarTag.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Fetch existing animals from database to check against already used tags, dam calvings, etc.
        var existingDbAnimals = await dbContext.Set<SubmissionAnimal>()
            .AsNoTracking()
            .Where(a => a.SubmissionId != submission.Id)
            .ToListAsync(cancellationToken);

        foreach (var animal in submission.Animals)
        {
            var errors = new List<ValidationErrorItem>();

            // CTWS070 / CTWS079: Location check
            if (!isCphValid)
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws079,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws079),
                });
            }

            // CTWS003: Missing Ear Tag
            if (string.IsNullOrWhiteSpace(animal.EarTag))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws003,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws003),
                });
            }
            else
            {
                // CTWS004: Invalid Ear Tag format (AANNNNNNNNNNNN)
                if (!earTagRegex.IsMatch(animal.EarTag.Trim()))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws004,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws004),
                    });
                }

                // CTWS204: Duplicate Ear Tag in file
                if (earTagGroups.Contains(animal.EarTag.Trim()))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws204,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws204),
                    });
                }

                // CTWS192: Ear Tag has already been used (existing in DB completed submissions or CADS)
                bool alreadyUsedInCads = cadsCattleList.Any(c =>
                    string.Equals(c.EarTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase));
                bool alreadyUsedInDb = existingDbAnimals.Any(a =>
                    string.Equals(a.EarTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    a.Status == Statuses.Complete);
                if (alreadyUsedInCads || alreadyUsedInDb)
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws192,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws192),
                    });
                }
            }

            // CTWS014: Invalid Breed Code
            if (animal.Breed != null && string.IsNullOrWhiteSpace(animal.Breed))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws014,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws014),
                });
            }

            // CTWS023: Birth Date cannot be in the future
            if (animal.DateBirth.HasValue)
            {
                if (animal.DateBirth.Value > today)
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws023,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws023),
                    });
                }
                else
                {
                    // CTWS203: Application is late
                    var daysSinceBirth = today.DayNumber - animal.DateBirth.Value.DayNumber;
                    if (daysSinceBirth > options.MaxApplicationLateDays)
                    {
                        errors.Add(new ValidationErrorItem
                        {
                            Code = ValidationRuleCodes.Ctws203,
                            Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws203),
                        });
                    }
                }
            }

            string? damEarTag;
            if (!string.IsNullOrWhiteSpace(animal.DamGeneticEarTag))
            {
                damEarTag = animal.DamGeneticEarTag.Trim();
            }
            else
            {
                damEarTag = !string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag)
                    ? animal.DamSurrogateEarTag.Trim()
                    : null;
            }

            // CTWS034: Genetic Dam and Animal Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                !string.IsNullOrWhiteSpace(animal.EarTag) &&
                string.Equals(animal.DamGeneticEarTag.Trim(), animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws034,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws034),
                });
            }

            // CTWS042: Surrogate Dam and Animal Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                !string.IsNullOrWhiteSpace(animal.EarTag) &&
                string.Equals(animal.DamSurrogateEarTag.Trim(), animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws042,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws042),
                });
            }

            // CTWS043: Surrogate and Genetic Dam Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                !string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                string.Equals(animal.DamSurrogateEarTag.Trim(), animal.DamGeneticEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.Ctws043,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws043),
                });
            }

            // Sire Checks
            if (!string.IsNullOrWhiteSpace(animal.SireEarTag))
            {
                var sireTag = animal.SireEarTag.Trim();

                // CTWS044: Invalid Sire Ear Tag
                if (!earTagRegex.IsMatch(sireTag))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws044,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws044),
                    });
                }

                // CTWS050: Sire and Animal Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.EarTag) &&
                    string.Equals(sireTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws050,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws050),
                    });
                }

                // CTWS051: Sire and Genetic Dam Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                    string.Equals(sireTag, animal.DamGeneticEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws051,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws051),
                    });
                }

                // CTWS052: Sire and Surrogate Dam Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                    string.Equals(sireTag, animal.DamSurrogateEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws052,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws052),
                    });
                }

                // CTWS196: Sire's sex is invalid (if sire is recorded in CADS or DB and is female)
                var sireRecord = cadsCattleList.FirstOrDefault(c => string.Equals(c.EarTag, sireTag, StringComparison.OrdinalIgnoreCase))
                                 ?? existingDbAnimals.Where(a => string.Equals(a.EarTag, sireTag, StringComparison.OrdinalIgnoreCase))
                                     .Select(a => new CattleResponse { EarTag = a.EarTag, Sex = a.Sex })
                                     .FirstOrDefault();

                if (sireRecord?.Sex != null &&
                    (sireRecord.Sex.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                     sireRecord.Sex.Equals("Female", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws196,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws196),
                    });
                }
            }

            // Dam checks (Age, Calving interval, Sex)
            if (!string.IsNullOrWhiteSpace(damEarTag))
            {
                var damRecord = cadsCattleList.FirstOrDefault(c => string.Equals(c.EarTag, damEarTag, StringComparison.OrdinalIgnoreCase))
                                ?? existingDbAnimals.Where(a => string.Equals(a.EarTag, damEarTag, StringComparison.OrdinalIgnoreCase))
                                    .Select(a => new CattleResponse { EarTag = a.EarTag, DateBirth = a.DateBirth, Sex = a.Sex })
                                    .FirstOrDefault();

                // CTWS195: Dam's sex is invalid (if dam is recorded as male)
                if (damRecord?.Sex != null &&
                    (damRecord.Sex.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                     damRecord.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.Ctws195,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws195),
                    });
                }

                // Dam age validation (CTWS202: Dam is too old or too young)
                if (animal.DateBirth.HasValue && damRecord?.DateBirth.HasValue == true)
                {
                    var damBirth = damRecord.DateBirth.Value;
                    var calfBirth = animal.DateBirth.Value;

                    var ageInMonths = ((calfBirth.Year - damBirth.Year) * 12) + (calfBirth.Month - damBirth.Month);
                    if (calfBirth.Day < damBirth.Day)
                    {
                        ageInMonths--;
                    }

                    var ageInYears = calfBirth.Year - damBirth.Year;
                    if (calfBirth.Month < damBirth.Month || (calfBirth.Month == damBirth.Month && calfBirth.Day < damBirth.Day))
                    {
                        ageInYears--;
                    }

                    if (ageInMonths < options.MinDamAgeInMonths || ageInYears > options.MaxDamAgeInYears)
                    {
                        errors.Add(new ValidationErrorItem
                        {
                            Code = ValidationRuleCodes.Ctws202,
                            Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws202),
                        });
                    }
                }

                // Calving interval validation (CTWS200: Dam has already given birth)
                if (animal.DateBirth.HasValue)
                {
                    // Check other calves from the same dam in the current submission
                    var siblingCalves = submission.Animals
                        .Where(a => a.Id != animal.Id && a.DateBirth.HasValue &&
                                    ((a.DamGeneticEarTag != null && string.Equals(a.DamGeneticEarTag.Trim(), damEarTag, StringComparison.OrdinalIgnoreCase)) ||
                                     (a.DamSurrogateEarTag != null && string.Equals(a.DamSurrogateEarTag.Trim(), damEarTag, StringComparison.OrdinalIgnoreCase))))
                        .ToList();

                    // Check existing calves in DB
                    var dbSiblingCalves = existingDbAnimals
                        .Where(a => a.DateBirth.HasValue &&
                                    ((a.DamGeneticEarTag != null && string.Equals(a.DamGeneticEarTag.Trim(), damEarTag, StringComparison.OrdinalIgnoreCase)) ||
                                     (a.DamSurrogateEarTag != null && string.Equals(a.DamSurrogateEarTag.Trim(), damEarTag, StringComparison.OrdinalIgnoreCase))))
                        .ToList();

                    var allCalvesDates = siblingCalves.Select(c => c.DateBirth!.Value)
                        .Concat(dbSiblingCalves.Select(c => c.DateBirth!.Value));

                    foreach (var otherBirthDate in allCalvesDates)
                    {
                        var diffDays = Math.Abs(animal.DateBirth.Value.DayNumber - otherBirthDate.DayNumber);

                        // If calves born to same dam within calving interval (e.g. 240 days), except same day twins/multiples (diffDays > 0 and < MinCalvingIntervalDays)
                        if (diffDays > 0 && diffDays < options.MinCalvingIntervalDays)
                        {
                            errors.Add(new ValidationErrorItem
                            {
                                Code = ValidationRuleCodes.Ctws200,
                                Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.Ctws200),
                            });
                            break;
                        }
                    }
                }
            }

            // Apply validation results to animal entity
            animal.ClearErrors();

            if (errors.Count > 0)
            {
                foreach (var err in errors)
                {
                    animal.AddError(err.Code, err.Description);
                }

                animal.UpdateStatus(Statuses.Error);
            }
            else
            {
                animal.UpdateStatus(Statuses.Complete);
            }

            var animalResult = new SubmissionAnimalValidationResult
            {
                AnimalId = animal.Id,
                EarTag = animal.EarTag,
                IsValid = errors.Count == 0,
                Errors = errors,
            };

            result.AnimalResults.Add(animalResult);
        }

        // Update submission status based on animals
        submission.RefreshStatusFromAnimals();

        result.IsValid = result.AnimalResults.All(a => a.IsValid);
        result.Status = submission.Status;
        result.ErrorCount = result.AnimalResults.Sum(a => a.Errors.Count);

        logger?.LogInformation(
            "Validation completed for submission {SubmissionId}. Status: {Status}, Errors: {ErrorCount}",
            submission.Id,
            submission.Status,
            result.ErrorCount);

        return result;
    }
}
