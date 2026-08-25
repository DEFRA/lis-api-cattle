namespace Lis.Cattle.Messaging;

public interface ISubmissionMessagePublisher
{
    Task PublishSubmissionForValidationAsync(SubmissionValidationMessage message, CancellationToken cancellationToken = default);
}
