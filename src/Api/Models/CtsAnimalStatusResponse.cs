namespace Lis.Cattle.Models;

public class CtsAnimalStatusResponse
{
    public string EarTag { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsClean => string.Equals(Status, "clean", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(Status, "complete", StringComparison.OrdinalIgnoreCase);

    public bool IsError => string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase);

    public List<CtsErrorResponse> Errors { get; set; } = [];
}
