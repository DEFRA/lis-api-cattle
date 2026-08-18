namespace Lis.Cattle.Models;

public class CattleResponse
{
    public string EarTag { get; set; } = string.Empty;
    public DateOnly? DateBirth { get; set; }
    public string? Sex { get; set; }
    public string? Breed { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CattleErrorResponse> Errors { get; set; } = new();
}

