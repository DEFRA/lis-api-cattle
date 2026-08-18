namespace Lis.Cattle.Models;

public class AnimalRegistrationRequest
{
    public string EarTag { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? Breed { get; set; }
    public DamRegistrationRequest? Dam { get; set; }
    public SireRegistrationRequest? Sire { get; set; }
}