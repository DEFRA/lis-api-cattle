using Lis.Cattle.Interfaces;
using Lis.Cattle.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;

namespace Lis.Cattle;

public class CtsBundlePollingJobTests
{
    [Fact]
    public async Task Execute_ResolvesProcessorAndExecutes()
    {
        var mockProcessor = new Mock<ICtsBundleProcessorService>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => mockProcessor.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var mockLogger = new Mock<ILogger<CtsBundlePollingJob>>();
        var job = new CtsBundlePollingJob(scopeFactory, mockLogger.Object);

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.CancellationToken).Returns(TestContext.Current.CancellationToken);

        await job.Execute(mockContext.Object);

        mockProcessor.Verify(p => p.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken), Times.Once);
    }
}
