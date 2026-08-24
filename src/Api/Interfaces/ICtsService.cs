using Lis.Cattle.Models;

namespace Lis.Cattle.Interfaces;

public interface ICtsService
{
    Task<CtsAnimalStatusResponse> SubmitAnimalRegistrationAsync(SubmissionAnimal animal, CancellationToken cancellationToken = default);

    Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default);
}
