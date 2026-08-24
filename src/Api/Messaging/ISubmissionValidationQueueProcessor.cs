namespace Lis.Cattle.Messaging;

public interface ISubmissionValidationQueueProcessor
{
    Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default);
}
