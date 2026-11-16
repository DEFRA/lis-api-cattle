// <copyright file="CattleService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Database.Postgres;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;

public class CattleService : ICattleService
{
    private readonly ICadsService cadsService;
    private readonly DbContext dbContext;

    public CattleService(ICadsService cadsService, PostgresDbContext dbContext)
        : this(cadsService, (DbContext)dbContext)
    {
    }

    public CattleService(ICadsService cadsService, DbContext dbContext)
    {
        this.cadsService = cadsService;
        this.dbContext = dbContext;
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleForHoldingAsync(string cph, CattleFilter? filter = null, CancellationToken cancellationToken = default)
    {
        // 1. Fetch from CADS
        var cadsCattle = await cadsService.GetCattleByCphAsync(cph, cancellationToken);
        var resultList = cadsCattle.ToList();

        // 2. Fetch from local database (bundle list for processing or error entries)
        // Bundles are typically submissions that are not yet "completed" or have errors.
        // Assuming status 'submitted' or presence of errors means they haven't been delivered to CADS yet.
        var localCattle = await dbContext.Set<SubmissionAnimal>()
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
            .ToListAsync(cancellationToken);

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

        // 4. Apply optional search filters after the merge so local records are searchable too
        if (filter is { IsEmpty: false })
        {
            return resultList.Where(filter.Matches).ToList();
        }

        return resultList;
    }

    public Task<CattleDetailsResponse> GetCattleDetailsAsync(string earTag, CancellationToken cancellationToken = default)
    {
        // Details come from CADS alone. An animal that exists only in a local submission bundle is
        // listed against its holding but has no CADS record yet, so it is reported as not found.
        return cadsService.GetAnimalDetailsAsync(earTag, cancellationToken);
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
}
