// <copyright file="CattleService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Database.Postgres;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CattleService : ICattleService
{
    private readonly ICadsService cadsService;
    private readonly DbContext dbContext;
    private readonly ISubmissionMessagePublisher? submissionMessagePublisher;
    private readonly ILogger<CattleService>? logger;

    public CattleService(ICadsService cadsService, PostgresDbContext dbContext)
        : this(cadsService, (DbContext)dbContext, null, null)
    {
    }

    public CattleService(ICadsService cadsService, DbContext dbContext)
        : this(cadsService, dbContext, null, null)
    {
    }

    public CattleService(
        ICadsService cadsService,
        PostgresDbContext dbContext,
        ISubmissionMessagePublisher? submissionMessagePublisher,
        ILogger<CattleService>? logger)
        : this(cadsService, (DbContext)dbContext, submissionMessagePublisher, logger)
    {
    }

    public CattleService(
        ICadsService cadsService,
        DbContext dbContext,
        ISubmissionMessagePublisher? submissionMessagePublisher,
        ILogger<CattleService>? logger)
    {
        this.cadsService = cadsService;
        this.dbContext = dbContext;
        this.submissionMessagePublisher = submissionMessagePublisher;
        this.logger = logger;
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleForHoldingAsync(string cph)
    {
        // 1. Fetch from CADS
        var cadsCattle = await cadsService.GetCattleByCphAsync(cph);
        var resultList = cadsCattle.ToList();

        // 2. Fetch from local database (bundle list for processing or error entries)
        // Bundles are typically submissions that are not yet "completed" or have errors.
        // Assuming status 'submitted' or presence of errors means they haven't been delivered to CADS yet.
        var localCattle = await dbContext.Set<SubmissionAnimal>()
            .Include(a => a.Errors)
            .Where(a => a.Submission.CountyParishHolding == cph &&
                        (a.Submission.Status == Statuses.Submitted || a.Errors.Any()))
            .Select(a => new CattleResponse
            {
                EarTag = a.EarTag,
                DateBirth = a.DateBirth,
                Sex = a.Sex,
                Breed = a.Breed,
                Status = a.Status,
                Errors = a.Errors.Select(e => new CattleErrorResponse
                {
                    ErrorCode = e.ErrorCode,
                    ErrorText = e.ErrorText,
                }).ToList(),
            })
            .ToListAsync();

        // 3. Enhance/Merge
        // The issue says: "The result from this list will then need to be enhanced with any details
        // that are held by the cattle API from the bundle list for processing or error entries"
        foreach (var localItem in localCattle)
        {
            var existing = resultList.FirstOrDefault(c => c.EarTag == localItem.EarTag);
            if (existing != null)
            {
                // Enhance existing CADS record with local details/errors
                existing.Status = localItem.Status;
                existing.Errors.AddRange(localItem.Errors);
            }
            else
            {
                // Add new local record that isn't in CADS yet
                resultList.Add(localItem);
            }
        }

        return resultList;
    }

    public async Task<IEnumerable<BundleResponse>> GetBundlesForHoldingAsync(string cph)
    {
        var submissions = await dbContext.Set<Submission>()
            .AsNoTracking()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .Where(s => s.CountyParishHolding == cph)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return submissions.Select(s => new BundleResponse
        {
            Id = s.Id,
            ClientReference = s.ClientReference,
            CountyParishHolding = s.CountyParishHolding,
            SubmittedBy = s.SubmittedBy,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            Animals = s.Animals.Select(a => new BundleAnimalResponse
            {
                Id = a.Id,
                SubmissionId = a.SubmissionId,
                Status = a.Status,
                EarTag = a.EarTag,
                DateBirth = a.DateBirth,
                Sex = a.Sex,
                Breed = a.Breed,
                DamType = a.DamType,
                DamGeneticEarTag = a.DamGeneticEarTag,
                DamSurrogateEarTag = a.DamSurrogateEarTag,
                SireEarTag = a.SireEarTag,
                SireName = a.SireName,
                Errors = a.Errors.Select(e => new BundleAnimalErrorResponse
                {
                    Id = e.Id,
                    AnimalId = e.AnimalId,
                    ErrorCode = e.ErrorCode,
                    ErrorText = e.ErrorText,
                    CreatedAt = e.CreatedAt,
                }).ToList(),
            }).ToList(),
        }).ToList();
    }

    public async Task<BundleResponse> CreateRegistrationBundleAsync(RegistrationBundleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientReference);
        ArgumentNullException.ThrowIfNull(request.Holding);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Holding.Cph);

        var submittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) ? "BE4FE" : request.SubmittedBy;

        var submission = new Submission(
            clientReference: request.ClientReference,
            countyParishHolding: request.Holding.Cph,
            submittedBy: submittedBy,
            status: Statuses.Pending);

        foreach (var animalRequest in request.Animals)
        {
            submission.AddAnimal(
                earTag: animalRequest.EarTag,
                status: Statuses.Pending,
                dateBirth: animalRequest.DateOfBirth,
                sex: animalRequest.Sex,
                breed: animalRequest.Breed,
                damType: animalRequest.Dam?.Type,
                damGeneticEarTag: animalRequest.Dam?.GeneticDamEarTag,
                damSurrogateEarTag: animalRequest.Dam?.SurrogateDamEarTag,
                sireEarTag: animalRequest.Sire?.EarTag,
                sireName: animalRequest.Sire?.Name);
        }

        await dbContext.Set<Submission>().AddAsync(submission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (submissionMessagePublisher != null)
        {
            try
            {
                await submissionMessagePublisher.PublishSubmissionForValidationAsync(
                    new SubmissionValidationMessage
                    {
                        SubmissionId = submission.Id,
                        CountyParishHolding = submission.CountyParishHolding,
                        ClientReference = submission.ClientReference,
                        SubmittedBy = submission.SubmittedBy,
                        AnimalCount = submission.Animals.Count,
                        Timestamp = submission.CreatedAt,
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to publish validation message for submission {SubmissionId}", submission.Id);
            }
        }

        return new BundleResponse
        {
            Id = submission.Id,
            ClientReference = submission.ClientReference,
            CountyParishHolding = submission.CountyParishHolding,
            SubmittedBy = submission.SubmittedBy,
            Status = submission.Status,
            CreatedAt = submission.CreatedAt,
            Animals = submission.Animals.Select(a => new BundleAnimalResponse
            {
                Id = a.Id,
                SubmissionId = a.SubmissionId,
                Status = a.Status,
                EarTag = a.EarTag,
                DateBirth = a.DateBirth,
                Sex = a.Sex,
                Breed = a.Breed,
                DamType = a.DamType,
                DamGeneticEarTag = a.DamGeneticEarTag,
                DamSurrogateEarTag = a.DamSurrogateEarTag,
                SireEarTag = a.SireEarTag,
                SireName = a.SireName,
                Errors = [],
            }).ToList(),
        };
    }
}
