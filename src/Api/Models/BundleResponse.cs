namespace Lis.Cattle.Models;

public class BundleResponse
{
    public Guid Id { get; set; }
    public string ClientReference { get; set; } = string.Empty;
    public string CountyParishHolding { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<BundleAnimalResponse> Animals { get; set; } = [];
}
