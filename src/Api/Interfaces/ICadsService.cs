using Lis.Cattle.Models;

namespace Lis.Cattle.Interfaces;

public interface ICadsService
{
    Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph);
}
