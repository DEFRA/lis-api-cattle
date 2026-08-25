using Lis.Cattle.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lis.Cattle.Messaging;

public class SubmissionValidationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AwsMessagingOptions _options;
    private readonly ILogger<SubmissionValidationBackgroundService>? _logger;

    public SubmissionValidationBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<AwsMessagingOptions>? options = null,
        ILogger<SubmissionValidationBackgroundService>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? new AwsMessagingOptions();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableBackgroundConsumer)
        {
            _logger?.LogInformation("SubmissionValidationBackgroundService is disabled.");
            return;
        }

        _logger?.LogInformation("SubmissionValidationBackgroundService started.");

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ISubmissionValidationQueueProcessor>();
                await processor.ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled error occurred in SubmissionValidationBackgroundService polling loop.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger?.LogInformation("SubmissionValidationBackgroundService stopped.");
    }
}
