using System.ComponentModel.DataAnnotations;

namespace Lis.Cattle;

public class SubmissionAnimal
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public Submission Submission { get; set; } = null!;

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string EarTag { get; set; } = string.Empty;

    public DateOnly? DateBirth { get; set; }

    public string? Sex { get; set; }

    public string? Breed { get; set; }

    public string? DamType { get; set; }

    public string? DamGeneticEarTag { get; set; }

    public string? DamSurrogateEarTag { get; set; }

    public string? SireEarTag { get; set; }

    public string? SireName { get; set; }

    public ICollection<SubmissionAnimalError> Errors { get; set; } = new List<SubmissionAnimalError>();
}
