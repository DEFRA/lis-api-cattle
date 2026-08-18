namespace Lis.Cattle.Models;

public class BundleAnimalErrorResponse
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorText { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
