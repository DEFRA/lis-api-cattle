namespace Lis.Cattle.Models;

public class RegistrationBundleRequest
{
    public string ClientReference { get; set; } = string.Empty;
    public HoldingRequest? Holding { get; set; }
    public string? SubmittedBy { get; set; }
    public List<AnimalRegistrationRequest> Animals { get; set; } = [];
}