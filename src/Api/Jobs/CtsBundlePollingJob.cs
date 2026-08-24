using Lis.Cattle.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Lis.Cattle.Jobs;

[DisallowConcurrentExecution]
public class CtsBundlePollingJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CtsBundlePollingJob> _logger;

    public CtsBundlePollingJob(IServiceScopeFactory scopeFactory, ILogger<CtsBundlePollingJob> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting CTS bundle polling job execution at {Time}", DateTimeOffset.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ICtsBundleProcessorService>();
            await processor.ProcessPendingBundlesAsync(context.CancellationToken);
            _logger.LogInformation("Finished CTS bundle polling job execution successfully at {Time}", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CTS bundle polling job execution failed.");
            throw;
        }
    }
}
