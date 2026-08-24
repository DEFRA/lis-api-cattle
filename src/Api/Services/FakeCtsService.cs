using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.Extensions.Logging;

namespace Lis.Cattle.Services;

public class FakeCtsService : ICtsService
{
    private readonly ILogger<FakeCtsService>? _logger;

    public FakeCtsService(ILogger<FakeCtsService>? logger = null)
    {
        _logger = logger;
    }

    public Task<CtsAnimalStatusResponse> SubmitAnimalRegistrationAsync(SubmissionAnimal animal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);

        _logger?.LogInformation("Fake CTS received submission for animal {AnimalId} with EarTag {EarTag}", animal.Id, animal.EarTag);

        if (animal.EarTag.Contains("SUBMIT_ERR", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = animal.EarTag,
                Status = "error",
                Errors =
                [
                    new CtsErrorResponse
                    {
                        ErrorCode = "CTS_SUBMIT_FAIL",
                        ErrorText = "Submission rejected by CTS validation"
                    }
                ]
            });
        }

        return Task.FromResult(new CtsAnimalStatusResponse
        {
            EarTag = animal.EarTag,
            Status = "processing"
        });
    }

    public Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);

        _logger?.LogInformation("Fake CTS checking status for animal {AnimalId} with EarTag {EarTag}", animalId, earTag);

        if (earTag.Contains("ERR", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = earTag,
                Status = "error",
                Errors =
                [
                    new CtsErrorResponse
                    {
                        ErrorCode = "CTS_VALIDATION_ERROR",
                        ErrorText = $"CTS issue detected for animal {earTag}"
                    }
                ]
            });
        }

        if (earTag.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CtsAnimalStatusResponse
            {
                EarTag = earTag,
                Status = "processing"
            });
        }

        return Task.FromResult(new CtsAnimalStatusResponse
        {
            EarTag = earTag,
            Status = "clean"
        });
    }
}
