namespace Lis.Cattle;

public class Submission
{
    private readonly List<SubmissionAnimal> _animals = [];

    private Submission()
    {
    }

    public Submission(string clientReference, string countyParishHolding, string submittedBy, string status = "submitted")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(countyParishHolding);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = Guid.NewGuid();
        ClientReference = clientReference;
        CountyParishHolding = countyParishHolding;
        SubmittedBy = submittedBy;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string ClientReference { get; private set; } = string.Empty;

    public string CountyParishHolding { get; private set; } = string.Empty;

    public string SubmittedBy { get; private set; } = string.Empty;

    public string Status { get; private set; } = "submitted";

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<SubmissionAnimal> Animals => _animals.AsReadOnly();

    public SubmissionAnimal AddAnimal(
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
        var animal = new SubmissionAnimal(
            Id,
            earTag,
            status,
            dateBirth,
            sex,
            breed,
            damType,
            damGeneticEarTag,
            damSurrogateEarTag,
            sireEarTag,
            sireName);

        _animals.Add(animal);
        return animal;
    }

    public void UpdateStatus(string newStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        Status = newStatus;
    }
}