using Defra.Database.Postgres;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.EntityFrameworkCore;

namespace Lis.Cattle.Services;

public class CattleService : ICattleService
{
    private readonly ICadsService _cadsService;
    private readonly DbContext _dbContext;

    public CattleService(ICadsService cadsService, ReadOnlyPostgresDbContext dbContext)
    {
        _cadsService = cadsService;
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<CattleResponse>> GetCattleForHoldingAsync(string cph)
    {
        // 1. Fetch from CADS
        var cadsCattle = await _cadsService.GetCattleByCphAsync(cph);
        var resultList = cadsCattle.ToList();

        // 2. Fetch from local database (bundle list for processing or error entries)
        // Bundles are typically submissions that are not yet "completed" or have errors.
        // Assuming status 'submitted' or presence of errors means they haven't been delivered to CADS yet.
        var localCattle = await _dbContext.Set<SubmissionAnimal>()
            .Include(a => a.Errors)
            .Where(a => a.Submission.CountyParishHolding == cph && 
                        (a.Submission.Status == "submitted" || a.Errors.Any()))
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
                    ErrorText = e.ErrorText
                }).ToList()
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
}
