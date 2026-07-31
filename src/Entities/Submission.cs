using System.ComponentModel.DataAnnotations;

namespace Lis.Cattle;

public class Submission
{
    public Guid Id { get; set; }

    [Required]
    public string ClientReference { get; set; } = string.Empty;

    [Required]
    public string CountyParishHolding { get; set; } = string.Empty;

    [Required]
    public string SubmittedBy { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "submitted";

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SubmissionAnimal> Animals { get; set; } = new List<SubmissionAnimal>();
}
