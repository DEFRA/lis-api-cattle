namespace Lis.Cattle.Validation;

public interface ISubmissionValidationService
{
    Task<SubmissionValidationResult> ValidateSubmissionAsync(Submission submission, CancellationToken cancellationToken = default);
    Task<SubmissionValidationResult> ValidateSubmissionByIdAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

public class SubmissionValidationResult
{
    public Guid SubmissionId { get; set; }
    public bool IsValid { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public List<SubmissionAnimalValidationResult> AnimalResults { get; set; } = [];
}

public class SubmissionAnimalValidationResult
{
    public Guid AnimalId { get; set; }
    public string EarTag { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<ValidationErrorItem> Errors { get; set; } = [];
}

public class ValidationErrorItem
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
