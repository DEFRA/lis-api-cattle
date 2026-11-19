// <copyright file="RegistrationBundleIntegrationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Tests.Messaging;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// The whole LREG-925 path: the publisher puts a bundle on LocalStack SQS, the queue processor reads it, and the
/// handler stores it in Postgres and sends it through <see cref="CtsWebClient"/> to an in-process CTS fake that
/// speaks the same TransferDataHex protocol as lis-fake-service's <c>/cts_ws</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RegistrationBundleIntegrationTests(PostgresFixture postgres, LocalStackFixture localStack)
    : IClassFixture<PostgresFixture>, IClassFixture<LocalStackFixture>
{
    private const string DthUser = "integration-dth";
    private const string DthSecretValue = "integration-dth-secret";
    private const string CtsOnlineUser = "int-cts";
    private const string CtsOnlineSecretValue = "int-cts-secret";

    [Fact]
    public async Task ABundleIsQueuedStoredSentToCtsAndItsReceiptStored()
    {
        var (queueUrl, _) = await localStack.CreateQueueAsync(visibilityTimeoutSeconds: 30, maxReceiveCount: 5);
        var cts = new CtsWebFake();
        var message = RegistrationBundleHandlerTests.Message(animals: 2);

        await CreatePublisher(queueUrl).PublishAsync(message, TestContext.Current.CancellationToken);
        await DrainAsync(CreateProcessor(queueUrl, cts), expected: 1);

        await using var db = postgres.CreateDbContext();
        var stored = await db.Set<Submission>().Include(s => s.Animals).Include(s => s.CtsRequest)
            .SingleAsync(s => s.Id == message.SubmissionId, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Submitted, stored.Status);
        Assert.Equal("corr-925", stored.CorrelationId);
        Assert.Equal(2, stored.Animals.Count);
        Assert.NotNull(stored.CtsReceiptNumber);
        Assert.NotNull(stored.CtsSentAt);
        Assert.NotNull(stored.CtsRequest);
        Assert.Equal(stored.CtsReceiptNumber, stored.CtsRequest.ReceiptNumber);
        Assert.DoesNotContain(CtsOnlineSecretValue, stored.CtsRequest.Payload, StringComparison.Ordinal);

        Assert.Equal(1, cts.Sends[message.SubmissionId.ToString()]);
        Assert.Equal("corr-925", Assert.Single(cts.CorrelationIds));
        Assert.Equal(0, await localStack.CountMessagesAsync(queueUrl));
    }

    [Fact]
    public async Task TwoInstancesWithRedeliveredMessages_StoreAndSendEachBundleExactlyOnce()
    {
        var (queueUrl, _) = await localStack.CreateQueueAsync(visibilityTimeoutSeconds: 30, maxReceiveCount: 5);
        var cts = new CtsWebFake { Delay = TimeSpan.FromMilliseconds(50) };
        var messages = Enumerable.Range(0, 8).Select(_ => RegistrationBundleHandlerTests.Message()).ToList();

        // Every bundle is on the queue twice, as SQS's at-least-once delivery allows.
        var publisher = CreatePublisher(queueUrl);
        foreach (var message in messages.Concat(messages))
        {
            await publisher.PublishAsync(message, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(
            DrainAsync(CreateProcessor(queueUrl, cts), expected: null),
            DrainAsync(CreateProcessor(queueUrl, cts), expected: null));

        await using var db = postgres.CreateDbContext();
        var ids = messages.Select(m => m.SubmissionId).ToList();
        var stored = await db.Set<Submission>().Where(s => ids.Contains(s.Id)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(messages.Count, stored.Count);
        Assert.All(stored, s => Assert.NotNull(s.CtsReceiptNumber));
        Assert.Equal(messages.Count, await db.Set<CtsRequest>().CountAsync(r => ids.Contains(r.SubmissionId), TestContext.Current.CancellationToken));
        Assert.Equal(messages.Count, cts.Sends.Count);
        Assert.All(cts.Sends.Values, count => Assert.Equal(1, count));
        Assert.Equal(0, await localStack.CountMessagesAsync(queueUrl));
    }

    [Fact]
    public async Task TwoDeliveriesOfOneBundleRacing_SendItToCtsOnce()
    {
        var cts = new CtsWebFake { Delay = TimeSpan.FromMilliseconds(200) };
        var message = RegistrationBundleHandlerTests.Message();

        var outcomes = await Task.WhenAll(HandleAsync(message, cts), HandleAsync(message, cts));

        Assert.Equal(1, cts.Sends[message.SubmissionId.ToString()]);
        Assert.Contains(RegistrationBundleOutcome.Sent, outcomes);
        Assert.Contains(RegistrationBundleOutcome.AlreadyHandled, outcomes);
    }

    [Fact]
    public async Task WhenCtsRejectsTheBundle_ItIsMarkedAsErroredAndTheMessageIsDeleted()
    {
        var (queueUrl, _) = await localStack.CreateQueueAsync(visibilityTimeoutSeconds: 30, maxReceiveCount: 5);
        var cts = new CtsWebFake { Reject = ("CTWS808", "Request rejected - 'data' XML does not conform to the 'data' XSD") };
        var message = RegistrationBundleHandlerTests.Message();

        await CreatePublisher(queueUrl).PublishAsync(message, TestContext.Current.CancellationToken);
        await DrainAsync(CreateProcessor(queueUrl, cts), expected: 1);

        await using var db = postgres.CreateDbContext();
        var stored = await db.Set<Submission>().SingleAsync(s => s.Id == message.SubmissionId, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Error, stored.Status);
        Assert.Equal("CTWS808", stored.ErrorCode);
        Assert.Null(stored.CtsReceiptNumber);
        Assert.Null(stored.CtsSentAt);
        Assert.Equal(0, await localStack.CountMessagesAsync(queueUrl));
    }

    [Fact]
    public async Task AMessageThatKeepsFailing_MovesToTheDeadLetterQueue()
    {
        var (queueUrl, deadLetterQueueUrl) = await localStack.CreateQueueAsync(visibilityTimeoutSeconds: 1, maxReceiveCount: 2);
        using (var sqs = localStack.CreateClient())
        {
            await sqs.SendMessageAsync(queueUrl, "{\"not\":\"a bundle\"}", TestContext.Current.CancellationToken);
        }

        var processor = CreateProcessor(queueUrl, new CtsWebFake(), visibilityTimeoutSeconds: 1, maxReceiveCount: 2);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && await localStack.CountMessagesAsync(deadLetterQueueUrl) == 0)
        {
            await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await localStack.CountMessagesAsync(deadLetterQueueUrl));
    }

    private static async Task DrainAsync(RegistrationBundleQueueProcessor processor, int? expected)
    {
        var handled = 0;
        var emptyPolls = 0;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline && (expected == null ? emptyPolls < 3 : handled < expected))
        {
            var count = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);
            handled += count;
            emptyPolls = count == 0 ? emptyPolls + 1 : 0;
        }

        if (expected != null)
        {
            Assert.Equal(expected, handled);
        }
    }

    private static CtsWebClient CreateCtsClient(CtsWebFake cts) => new(
        new HttpClient(cts),
        Options.Create(new CtsWebOptions
        {
            Url = "http://cts.test/cts_ws/DefraDataTransferPublicNWSE.asmx",
            ServiceName = "DEFRA-CTWS-FULL-PROVING",
            DthUsername = DthUser,
            DthPassword = DthSecretValue,
            CtsOnlineUsername = CtsOnlineUser,
            CtsOnlinePassword = CtsOnlineSecretValue,
            RetryDelayMilliseconds = 0,
        }),
        NullLogger<CtsWebClient>.Instance);

    private static RegistrationBundleHandler CreateHandler(DbContext db, CtsWebFake cts) =>
        new(db, new PostgresSubmissionLocker(), CreateCtsClient(cts), TimeProvider.System, NullLogger<RegistrationBundleHandler>.Instance);

    private async Task<RegistrationBundleOutcome> HandleAsync(RegistrationBundleMessage message, CtsWebFake cts)
    {
        await using var db = postgres.CreateRetryingDbContext();
        return await CreateHandler(db, cts).HandleAsync(message, TestContext.Current.CancellationToken);
    }

    private RegistrationBundlePublisher CreatePublisher(string queueUrl) =>
        new(localStack.CreateClient(), Options.Create(new AwsMessagingOptions { RegistrationBundleQueueUrl = queueUrl }), NullLogger<RegistrationBundlePublisher>.Instance);

    private RegistrationBundleQueueProcessor CreateProcessor(string queueUrl, CtsWebFake cts, int visibilityTimeoutSeconds = 30, int maxReceiveCount = 5)
    {
        var services = new ServiceCollection();
        services.AddScoped<DbContext>(_ => postgres.CreateRetryingDbContext());
        services.AddScoped<IRegistrationBundleHandler>(sp => CreateHandler(sp.GetRequiredService<DbContext>(), cts));
        var provider = services.BuildServiceProvider();

        return new RegistrationBundleQueueProcessor(
            localStack.CreateClient(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AwsMessagingOptions
            {
                RegistrationBundleQueueUrl = queueUrl,
                RegistrationBundleVisibilityTimeoutSeconds = visibilityTimeoutSeconds,
                RegistrationBundleMaxReceiveCount = maxReceiveCount,
                WaitTimeSeconds = 1,
            }),
            NullLogger<RegistrationBundleQueueProcessor>.Instance);
    }

    /// <summary>
    /// Replays lis-fake-service's cts_ws behaviour that matters here: the TransferDataHex and CTS Online
    /// credentials are checked, a TxnId is accepted once (a resend gets CTWS807), and a receipt is returned.
    /// </summary>
    private sealed class CtsWebFake : HttpMessageHandler
    {
        private long nextReceipt = 1000;

        public ConcurrentDictionary<string, int> Sends { get; } = new();

        public ConcurrentBag<string> CorrelationIds { get; } = [];

        public TimeSpan Delay { get; init; } = TimeSpan.Zero;

        public (string ExNum, string ExMsg)? Reject { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues(TraceHeaders.CdpRequestId, out var correlation))
            {
                CorrelationIds.Add(correlation.Single());
            }

            var envelope = XElement.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var transfer = envelope.Descendants(CtsWebClient.DefraNamespace + "TransferDataHex").Single();
            if (transfer.Element(CtsWebClient.DefraNamespace + "username")?.Value != DthUser
                || transfer.Element(CtsWebClient.DefraNamespace + "password")?.Value != Md5(DthSecretValue))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault><faultcode>soap:Client</faultcode><faultstring>Authentication failed</faultstring></soap:Fault></soap:Body></soap:Envelope>") };
            }

            var regBirths = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(transfer.Element(CtsWebClient.DefraNamespace + "data")!.Value)));
            var user = regBirths.Element(CtsBirthRequestBuilder.Namespace + "Authentication")?.Element("CTS_OL_User");
            if (user?.Attribute("Usr")?.Value != CtsOnlineUser || user.Attribute("Pwd")?.Value != CtsOnlineSecretValue)
            {
                return Result("<SystemException ExNum=\"CTWS001\" ExMsg=\"Authentication failed\"/>");
            }

            await Task.Delay(Delay, cancellationToken);

            if (Reject is { } reject)
            {
                return Result($"<SystemException ExNum=\"{reject.ExNum}\" ExMsg=\"{reject.ExMsg}\"/>");
            }

            var txnId = regBirths.Element(CtsBirthRequestBuilder.Namespace + "Births")!.Attribute("TxnId")!.Value;
            if (Sends.AddOrUpdate(txnId, 1, (_, count) => count + 1) > 1)
            {
                return Result("<SystemException ExNum=\"CTWS807\" ExMsg=\"Request rejected - already submitted\"/>");
            }

            return Result($"<MsgReceipt xmlns=\"http://defra.bcms.ctws/asynchronous_receipt\"><Receipt Num=\"{Interlocked.Increment(ref nextReceipt)}\"/></MsgReceipt>");
        }

        private static HttpResponseMessage Result(string inner)
        {
            var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + inner));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><TransferDataHexResponse xmlns=\"http://www.defra.gov.uk\"><TransferDataHexResult>{data}</TransferDataHexResult></TransferDataHexResponse></soap:Body></soap:Envelope>",
                    Encoding.UTF8,
                    "text/xml"),
            };
        }

#pragma warning disable CA5351, S4790
        private static string Md5(string value) => Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(value)));
#pragma warning restore CA5351, S4790
    }
}
