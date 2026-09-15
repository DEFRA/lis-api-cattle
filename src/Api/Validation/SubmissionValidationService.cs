// <copyright file="SubmissionValidationService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

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

        var context = await BuildContextAsync(submission, cancellationToken);

        var result = new SubmissionValidationResult
        {
            SubmissionId = submission.Id,
        };

        foreach (var animal in submission.Animals)
        {
            var errors = SubmissionAnimalRules.Validate(animal, context);
            ApplyErrors(animal, errors);

            result.AnimalResults.Add(new SubmissionAnimalValidationResult
            {
                AnimalId = animal.Id,
                EarTag = animal.EarTag,
                IsValid = errors.Count == 0,
                Errors = errors,
            });
        }

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

    private static void ApplyErrors(SubmissionAnimal animal, List<ValidationErrorItem> errors)
    {
        animal.ClearErrors();

        if (errors.Count == 0)
        {
            animal.UpdateStatus(Statuses.Complete);
            return;
        }

        foreach (var error in errors)
        {
            animal.AddError(error.Code, error.Description);
        }

        animal.UpdateStatus(Statuses.Error);
    }

    private async Task<SubmissionValidationContext> BuildContextAsync(Submission submission, CancellationToken cancellationToken)
    {
        var cadsCattle = await GetCadsCattleAsync(submission.CountyParishHolding, cancellationToken);

        // Animals held by other submissions: already used tags, dam details and earlier calvings.
        var existingAnimals = await dbContext.Set<SubmissionAnimal>()
            .AsNoTracking()
            .Where(a => a.SubmissionId != submission.Id)
            .ToListAsync(cancellationToken);

        return new SubmissionValidationContext(
            submission,
            options,
            cadsCattle,
            existingAnimals,
            DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // A CADS failure must not block validation; the tag-in-use and parentage checks simply see no CADS animals.
    private async Task<IReadOnlyList<CattleResponse>> GetCadsCattleAsync(string? cph, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cph))
        {
            return [];
        }

        try
        {
            return (await cadsService.GetCattleByCphAsync(cph, cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not fetch CADS cattle for holding {Cph}", cph);
            return [];
        }
    }
}
