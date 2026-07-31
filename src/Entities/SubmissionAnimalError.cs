using System.ComponentModel.DataAnnotations;
using Defra.Database.Entities;

namespace Lis.Cattle;

public class SubmissionAnimalError : BaseAuditEntity
{


    public Guid AnimalId { get; set; }

    public SubmissionAnimal Animal { get; set; } = null!;

    [Required]
    public string ErrorCode { get; set; } = string.Empty;

    [Required]
    public string ErrorText { get; set; } = string.Empty;
    
}
