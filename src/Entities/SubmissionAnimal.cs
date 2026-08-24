namespace Lis.Cattle;

public class SubmissionAnimal
{
    private readonly List<SubmissionAnimalError> _errors = [];

    private SubmissionAnimal()
    {
    }

    public SubmissionAnimal(
        Guid submissionId,
        string earTag,
        string status = "submitted",
        DateOnly? dateBirth = null,
        string? sex = null,
        string? breed = null,
        string? damType = null,
        string? damGeneticEarTag = null,
        string? damSurrogateEarTag = null,
        string? sireEarTag = null,
        string? sireName = null)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission ID must be valid.", nameof(submissionId));

        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = Guid.NewGuid();
        SubmissionId = submissionId;
        EarTag = earTag ?? string.Empty;
        Status = status;
        DateBirth = dateBirth;
        Sex = sex;
        Breed = breed;
        DamType = damType;
        DamGeneticEarTag = damGeneticEarTag;
        DamSurrogateEarTag = damSurrogateEarTag;
        SireEarTag = sireEarTag;
        SireName = sireName;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public Submission Submission { get; private set; } = null!;

    public string Status { get; private set; } = string.Empty;

    public string EarTag { get; private set; } = string.Empty;

    public DateOnly? DateBirth { get; private set; }

    public string? Sex { get; private set; }

    public string? Breed { get; private set; }

    public string? DamType { get; private set; }

    public string? DamGeneticEarTag { get; private set; }

    public string? DamSurrogateEarTag { get; private set; }

    public string? SireEarTag { get; private set; }

    public string? SireName { get; private set; }

    public IReadOnlyCollection<SubmissionAnimalError> Errors => _errors.AsReadOnly();

    public SubmissionAnimalError AddError(string errorCode, string errorText)
    {
        var error = new SubmissionAnimalError(Id, errorCode, errorText);
        _errors.Add(error);
        return error;
    }

    public void UpdateStatus(string newStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        Status = newStatus;
    }

    public void MarkAsProcessing()
    {
        Status = "processing";
    }

    public void MarkAsComplete()
    {
        Status = "complete";
        _errors.Clear();
    }

    public void MarkAsError(string errorCode, string errorText)
    {
        Status = "error";
        AddError(errorCode, errorText);
    }

    public void ClearErrors()
    {
        _errors.Clear();
    }
}