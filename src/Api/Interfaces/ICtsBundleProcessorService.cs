namespace Lis.Cattle.Interfaces;

public interface ICtsBundleProcessorService
{
    Task ProcessPendingBundlesAsync(CancellationToken cancellationToken = default);
}
