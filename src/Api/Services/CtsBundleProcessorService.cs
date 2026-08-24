using Defra.Database.Postgres;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lis.Cattle.Services;

public class CtsBundleProcessorService : ICtsBundleProcessorService
{
    private readonly ICtsService _ctsService;
    private readonly DbContext _dbContext;
    private readonly CtsPollingJobOptions _options;
    private readonly ILogger<CtsBundleProcessorService>? _logger;

    public CtsBundleProcessorService(
        ICtsService ctsService,
        PostgresDbContext dbContext,
        IOptions<CtsPollingJobOptions>? options = null,
        ILogger<CtsBundleProcessorService>? logger = null)
        : this(ctsService, (DbContext)dbContext, options, logger)
    {
    }

    public CtsBundleProcessorService(
        ICtsService ctsService,
        DbContext dbContext,
        IOptions<CtsPollingJobOptions>? options = null,
        ILogger<CtsBundleProcessorService>? logger = null)
    {
        _ctsService = ctsService ?? throw new ArgumentNullException(nameof(ctsService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? new CtsPollingJobOptions();
        _logger = logger;
    }

    public async Task ProcessPendingBundlesAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 10;

        var targetStatuses = new[] { "submitted", "processing", "error", "pending" };

        var bundles = await _dbContext.Set<Submission>()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .Where(s => targetStatuses.Contains(s.Status))
            .OrderBy(s => s.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (bundles.Count == 0)
        {
            _logger?.LogDebug("No pending bundles found to process.");
            return;
        }

        _logger?.LogInformation("Found {Count} bundles to process with CTS.", bundles.Count);

        foreach (var bundle in bundles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessBundleAsync(bundle, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing bundle {BundleId}", bundle.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessBundleAsync(Submission bundle, CancellationToken cancellationToken)
    {
        if (bundle.Status == "submitted" || bundle.Status == "pending")
        {
            bundle.MarkAsProcessing();

            foreach (var animal in bundle.Animals)
            {
                var response = await _ctsService.SubmitAnimalRegistrationAsync(animal, cancellationToken);
                if (response.IsError)
                {
                    animal.MarkAsError(
                        response.Errors.FirstOrDefault()?.ErrorCode ?? "CTS_ERR",
                        response.Errors.FirstOrDefault()?.ErrorText ?? "CTS Submission Error");
                }
                else
                {
                    animal.MarkAsProcessing();
                }
            }
        }
        else if (bundle.Status == "processing" || bundle.Status == "error")
        {
            var targetAnimals = bundle.Animals
                .Where(a => a.Status == "processing" || a.Status == "error" || a.Status == "submitted" || a.Status == "pending")
                .ToList();

            foreach (var animal in targetAnimals)
            {
                var response = await _ctsService.CheckAnimalStatusAsync(animal.EarTag, animal.Id, cancellationToken);

                if (response.IsClean)
                {
                    animal.MarkAsComplete();
                }
                else if (response.IsError)
                {
                    var errorCode = response.Errors.FirstOrDefault()?.ErrorCode ?? "CTS_ERR";
                    var errorText = response.Errors.FirstOrDefault()?.ErrorText ?? "CTS Validation Error";
                    animal.MarkAsError(errorCode, errorText);
                }
                else
                {
                    animal.MarkAsProcessing();
                }
            }
        }

        bundle.RefreshStatusFromAnimals();
    }
}
