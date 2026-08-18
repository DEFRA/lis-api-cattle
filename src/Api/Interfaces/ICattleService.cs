using Lis.Cattle.Models;

namespace Lis.Cattle.Interfaces;

public interface ICattleService
{
    Task<IEnumerable<CattleResponse>> GetCattleForHoldingAsync(string cph);
    Task<IEnumerable<BundleResponse>> GetBundlesForHoldingAsync(string cph);
}