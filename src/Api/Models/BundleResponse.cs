namespace Lis.Cattle.Models;

public class BundleResponse
{
    public Guid Id { get; set; }
    public string ClientReference { get; set; } = string.Empty;
    public string CountyParishHolding { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<BundleAnimalResponse> Animals { get; set; } = new();
}

public class BundleAnimalResponse
{
    public Guid Id { get; set; }
    public string EarTag { get; set; } = string.Empty;
    public DateOnly? DateBirth { get; set; }
    public string? Sex { get; set; }
    public string? Breed { get; set; }
    public string? DamType { get; set; }
    public string? DamGeneticEarTag { get; set; }
    public string? DamSurrogateEarTag { get; set; }
    public string? SireEarTag { get; set; }
    public string? SireName { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CattleErrorResponse> Errors { get; set; } = new();
}
