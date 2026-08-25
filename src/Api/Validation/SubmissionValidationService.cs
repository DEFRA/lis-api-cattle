using System.Text.RegularExpressions;
using Lis.Cattle.Configurations;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lis.Cattle.Validation;

public class SubmissionValidationService : ISubmissionValidationService
{
    private readonly DbContext _dbContext;
    private readonly ICadsService _cadsService;
    private readonly SubmissionValidationOptions _options;
    private readonly ILogger<SubmissionValidationService>? _logger;

    public SubmissionValidationService(
        DbContext dbContext,
        ICadsService cadsService,
        IOptions<SubmissionValidationOptions>? options = null,
        ILogger<SubmissionValidationService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cadsService = cadsService ?? throw new ArgumentNullException(nameof(cadsService));
        _options = options?.Value ?? new SubmissionValidationOptions();
        _logger = logger;
    }

    public async Task<SubmissionValidationResult> ValidateSubmissionByIdAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _dbContext.Set<Submission>()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

        if (submission == null)
        {
            _logger?.LogWarning("Submission with ID {SubmissionId} not found for validation", submissionId);
            throw new KeyNotFoundException($"Submission with ID {submissionId} not found.");
        }

        var result = await ValidateSubmissionAsync(submission, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<SubmissionValidationResult> ValidateSubmissionAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        _logger?.LogInformation("Starting validation for submission {SubmissionId} (CPH: {Cph})", submission.Id, submission.CountyParishHolding);

        var result = new SubmissionValidationResult
        {
            SubmissionId = submission.Id,
            IsValid = true
        };

        var earTagRegex = new Regex(_options.EarTagRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var cphRegex = new Regex(_options.CphRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Location / CPH format validation
        bool isCphValid = !string.IsNullOrWhiteSpace(submission.CountyParishHolding) && cphRegex.IsMatch(submission.CountyParishHolding);

        // Fetch CADS cattle for this CPH and holding history
        IEnumerable<CattleResponse> cadsCattle = [];
        try
        {
            if (!string.IsNullOrWhiteSpace(submission.CountyParishHolding))
            {
                cadsCattle = await _cadsService.GetCattleByCphAsync(submission.CountyParishHolding);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not fetch CADS cattle for holding {Cph}", submission.CountyParishHolding);
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
        var existingDbAnimals = await _dbContext.Set<SubmissionAnimal>()
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
                    Code = ValidationRuleCodes.CTWS079,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS079)
                });
            }

            // CTWS003: Missing Ear Tag
            if (string.IsNullOrWhiteSpace(animal.EarTag))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.CTWS003,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS003)
                });
            }
            else
            {
                // CTWS004: Invalid Ear Tag format (AANNNNNNNNNNNN)
                if (!earTagRegex.IsMatch(animal.EarTag.Trim()))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS004,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS004)
                    });
                }

                // CTWS204: Duplicate Ear Tag in file
                if (earTagGroups.Contains(animal.EarTag.Trim()))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS204,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS204)
                    });
                }

                // CTWS192: Ear Tag has already been used (existing in DB completed submissions or CADS)
                bool alreadyUsedInCads = cadsCattleList.Any(c => string.Equals(c.EarTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase));
                bool alreadyUsedInDb = existingDbAnimals.Any(a => string.Equals(a.EarTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase) && a.Status == "complete");
                if (alreadyUsedInCads || alreadyUsedInDb)
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS192,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS192)
                    });
                }
            }

            // CTWS014: Invalid Breed Code
            if (animal.Breed != null && string.IsNullOrWhiteSpace(animal.Breed))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.CTWS014,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS014)
                });
            }

            // CTWS023: Birth Date cannot be in the future
            if (animal.DateBirth.HasValue)
            {
                if (animal.DateBirth.Value > today)
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS023,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS023)
                    });
                }
                else
                {
                    // CTWS203: Application is late
                    var daysSinceBirth = today.DayNumber - animal.DateBirth.Value.DayNumber;
                    if (daysSinceBirth > _options.MaxApplicationLateDays)
                    {
                        errors.Add(new ValidationErrorItem
                        {
                            Code = ValidationRuleCodes.CTWS203,
                            Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS203)
                        });
                    }
                }
            }

            var damEarTag = !string.IsNullOrWhiteSpace(animal.DamGeneticEarTag)
                ? animal.DamGeneticEarTag.Trim()
                : (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) ? animal.DamSurrogateEarTag.Trim() : null);

            // CTWS034: Genetic Dam and Animal Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                !string.IsNullOrWhiteSpace(animal.EarTag) &&
                string.Equals(animal.DamGeneticEarTag.Trim(), animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.CTWS034,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS034)
                });
            }

            // CTWS042: Surrogate Dam and Animal Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                !string.IsNullOrWhiteSpace(animal.EarTag) &&
                string.Equals(animal.DamSurrogateEarTag.Trim(), animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.CTWS042,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS042)
                });
            }

            // CTWS043: Surrogate and Genetic Dam Ear Tags match
            if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                !string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                string.Equals(animal.DamSurrogateEarTag.Trim(), animal.DamGeneticEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationErrorItem
                {
                    Code = ValidationRuleCodes.CTWS043,
                    Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS043)
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
                        Code = ValidationRuleCodes.CTWS044,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS044)
                    });
                }

                // CTWS050: Sire and Animal Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.EarTag) &&
                    string.Equals(sireTag, animal.EarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS050,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS050)
                    });
                }

                // CTWS051: Sire and Genetic Dam Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.DamGeneticEarTag) &&
                    string.Equals(sireTag, animal.DamGeneticEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS051,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS051)
                    });
                }

                // CTWS052: Sire and Surrogate Dam Ear Tags match
                if (!string.IsNullOrWhiteSpace(animal.DamSurrogateEarTag) &&
                    string.Equals(sireTag, animal.DamSurrogateEarTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Code = ValidationRuleCodes.CTWS052,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS052)
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
                        Code = ValidationRuleCodes.CTWS196,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS196)
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
                        Code = ValidationRuleCodes.CTWS195,
                        Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS195)
                    });
                }

                // Dam age validation (CTWS202: Dam is too old or too young)
                if (animal.DateBirth.HasValue && damRecord?.DateBirth.HasValue == true)
                {
                    var damBirth = damRecord.DateBirth.Value;
                    var calfBirth = animal.DateBirth.Value;

                    var ageInMonths = (calfBirth.Year - damBirth.Year) * 12 + (calfBirth.Month - damBirth.Month);
                    if (calfBirth.Day < damBirth.Day)
                    {
                        ageInMonths--;
                    }

                    var ageInYears = calfBirth.Year - damBirth.Year;
                    if (calfBirth.Month < damBirth.Month || (calfBirth.Month == damBirth.Month && calfBirth.Day < damBirth.Day))
                    {
                        ageInYears--;
                    }

                    if (ageInMonths < _options.MinDamAgeInMonths || ageInYears > _options.MaxDamAgeInYears)
                    {
                        errors.Add(new ValidationErrorItem
                        {
                            Code = ValidationRuleCodes.CTWS202,
                            Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS202)
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
                        if (diffDays > 0 && diffDays < _options.MinCalvingIntervalDays)
                        {
                            errors.Add(new ValidationErrorItem
                            {
                                Code = ValidationRuleCodes.CTWS200,
                                Description = ValidationRuleCodes.GetDescription(ValidationRuleCodes.CTWS200)
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
                animal.UpdateStatus("error");
            }
            else
            {
                animal.UpdateStatus("complete");
            }

            var animalResult = new SubmissionAnimalValidationResult
            {
                AnimalId = animal.Id,
                EarTag = animal.EarTag,
                IsValid = errors.Count == 0,
                Errors = errors
            };

            result.AnimalResults.Add(animalResult);
        }

        // Update submission status based on animals
        submission.RefreshStatusFromAnimals();

        result.IsValid = result.AnimalResults.All(a => a.IsValid);
        result.Status = submission.Status;
        result.ErrorCount = result.AnimalResults.Sum(a => a.Errors.Count);

        _logger?.LogInformation(
            "Validation completed for submission {SubmissionId}. Status: {Status}, Errors: {ErrorCount}",
            submission.Id,
            submission.Status,
            result.ErrorCount);

        return result;
    }
}
