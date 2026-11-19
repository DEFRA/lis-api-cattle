// <copyright file="CtsWebClientTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Cts;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Cts;
using Microsoft.Extensions.Logging.Abstractions;

public class CtsWebClientTests
{
    private const string DthPasswordValue = "dth-secret";
    private const string CtsOnlinePasswordValue = "cts-secret";

    private static readonly XNamespace Births = CtsBirthRequestBuilder.Namespace;

    private readonly CtsWebOptions options = new()
    {
        Url = "http://cts.test/cts_ws/DefraDataTransferPublicNWSE.asmx",
        ServiceName = "DEFRA-CTWS-FULL-PROVING",
        DthUsername = "dth-user",
        DthPassword = DthPasswordValue,
        CtsOnlineUsername = "cts-user",
        CtsOnlinePassword = CtsOnlinePasswordValue,
        MaxAttempts = 3,
        RetryDelayMilliseconds = 0,
        TimeoutMilliseconds = 5_000,
    };

    [Fact]
    public async Task SubmitAsync_SendsATransferDataHexEnvelopeWithTheRequestAndCredentials()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, Receipt(4242));
        var client = CreateClient(handler);
        var payload = Payload();

        CorrelationIdContext.Value = "corr-cts";
        try
        {
            await client.SubmitAsync(CtsBirthRequestBuilder.Operation, payload, TestContext.Current.CancellationToken);
        }
        finally
        {
            CorrelationIdContext.Value = null;
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(options.Url, request.RequestUri?.ToString());
        Assert.Equal("corr-cts", Assert.Single(request.Headers.GetValues(TraceHeaders.CdpRequestId)));
        Assert.Contains("TransferDataHex", Assert.Single(request.Headers.GetValues("SOAPAction")), StringComparison.Ordinal);

        Assert.StartsWith("<?xml version='1.0' encoding='utf-8'?><soap:Envelope", handler.Bodies[0], StringComparison.Ordinal);
        var envelope = XElement.Parse(handler.Bodies[0]);
        var transfer = envelope.Descendants(CtsWebClient.DefraNamespace + "TransferDataHex").Single();
        Assert.Equal("soap", envelope.GetPrefixOfNamespace(CtsWebClient.SoapNamespace));
        Assert.Equal("dth-user", transfer.Element(CtsWebClient.DefraNamespace + "username")?.Value);
        Assert.Equal(Md5(DthPasswordValue), transfer.Element(CtsWebClient.DefraNamespace + "password")?.Value);
        Assert.Equal("DEFRA-CTWS-FULL-PROVING", transfer.Element(CtsWebClient.DefraNamespace + "serviceName")?.Value);
        Assert.Equal(CtsBirthRequestBuilder.Operation, transfer.Element(CtsWebClient.DefraNamespace + "type")?.Value);

        var data = Encoding.UTF8.GetString(Convert.FromBase64String(transfer.Element(CtsWebClient.DefraNamespace + "data")!.Value));
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?><RegBirths", data, StringComparison.Ordinal);
        var regBirths = XElement.Parse(data);
        var user = regBirths.Element(Births + "Authentication")?.Element("CTS_OL_User");
        Assert.NotNull(user);
        Assert.Equal("cts-user", user.Attribute("Usr")?.Value);
        Assert.Equal(CtsOnlinePasswordValue, user.Attribute("Pwd")?.Value);
        Assert.Equal(regBirths.Elements().First(), regBirths.Element(Births + "Authentication"));
        Assert.NotNull(regBirths.Element(Births + "Births"));
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsReturnsAReceipt_ReturnsTheReceiptNumber()
    {
        var client = CreateClient(new CtsStubHandler().Respond(HttpStatusCode.OK, Receipt(987654)));

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.True(result.IsAccepted);
        Assert.Equal(987654, result.ReceiptNumber);
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsRejectsTheRequest_ReturnsTheErrorWithoutRetrying()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, SystemException("CTWS808", "Request rejected - 'data' XML does not conform to the 'data' XSD"));
        var client = CreateClient(handler);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.False(result.IsAccepted);
        Assert.False(result.IsTransient);
        Assert.Equal("CTWS808", result.ErrorCode);
        Assert.Contains("does not conform", result.ErrorText, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsSaysAlreadySubmitted_ReturnsCtws807WithoutRetrying()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, SystemException("CTWS807", "Request rejected - already submitted"));
        var client = CreateClient(handler);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.Equal(CtsSubmitResult.AlreadySubmittedExNum, result.ErrorCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsIsBrieflyUnavailable_RetriesAndReturnsTheReceipt()
    {
        var handler = new CtsStubHandler()
            .Respond(HttpStatusCode.OK, SystemException("CTWS809", "Request rejected - service unavailable"))
            .Respond(HttpStatusCode.ServiceUnavailable, "<html>busy</html>")
            .Respond(HttpStatusCode.OK, Receipt(11));
        var client = CreateClient(handler);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.True(result.IsAccepted);
        Assert.Equal(11, result.ReceiptNumber);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsStaysUnavailable_ReturnsATransientFailureAfterMaxAttempts()
    {
        var handler = new CtsStubHandler().Fail().Fail().Fail();
        var client = CreateClient(handler);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.False(result.IsAccepted);
        Assert.True(result.IsTransient);
        Assert.Equal(CtsSubmitResult.UnavailableCode, result.ErrorCode);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SubmitAsync_WhenTheTransferCredentialsAreRejected_ReturnsTheFaultWithoutRetrying()
    {
        const string Fault = """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Client</faultcode><faultstring>Authentication failed</faultstring></soap:Fault></soap:Body></soap:Envelope>""";
        var handler = new CtsStubHandler().Respond(HttpStatusCode.InternalServerError, Fault);
        var client = CreateClient(handler);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.Equal(CtsSubmitResult.FaultCode, result.ErrorCode);
        Assert.Contains("Authentication failed", result.ErrorText, StringComparison.Ordinal);
        Assert.False(result.IsTransient);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WhenCtsDoesNotAnswerInTime_ReturnsATransientFailure()
    {
        var slowOptions = new CtsWebOptions
        {
            Url = options.Url,
            ServiceName = options.ServiceName,
            DthUsername = options.DthUsername,
            DthPassword = options.DthPassword,
            CtsOnlineUsername = options.CtsOnlineUsername,
            CtsOnlinePassword = options.CtsOnlinePassword,
            MaxAttempts = 1,
            TimeoutMilliseconds = 50,
        };
        var handler = new CtsStubHandler().Hang();
        var client = new CtsWebClient(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(slowOptions), NullLogger<CtsWebClient>.Instance);

        var result = await client.SubmitAsync(CtsBirthRequestBuilder.Operation, Payload(), TestContext.Current.CancellationToken);

        Assert.True(result.IsTransient);
        Assert.Equal(CtsSubmitResult.UnavailableCode, result.ErrorCode);
    }

    [Theory]
    [InlineData("<MsgReceipt xmlns=\"http://defra.bcms.ctws/asynchronous_receipt\"><Receipt Num=\"not-a-number\"/></MsgReceipt>")]
    [InlineData("<Something/>")]
    public void ParseResponse_WhenTheResultIsNotAReceiptOrException_ReturnsAnInvalidResponse(string inner)
    {
        var result = CtsWebClient.ParseResponse(HttpStatusCode.OK, Wrap(inner));

        Assert.Equal(CtsSubmitResult.InvalidResponseCode, result.ErrorCode);
        Assert.False(result.IsTransient);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "<html>down</html>", CtsSubmitResult.UnavailableCode, true)]
    [InlineData(HttpStatusCode.BadRequest, "", CtsSubmitResult.InvalidResponseCode, false)]
    [InlineData(HttpStatusCode.TooManyRequests, "busy", CtsSubmitResult.InvalidResponseCode, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "<busy/>", CtsSubmitResult.UnavailableCode, true)]
    [InlineData(HttpStatusCode.NotFound, "<missing/>", CtsSubmitResult.InvalidResponseCode, false)]
    [InlineData(HttpStatusCode.OK, "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body/></soap:Envelope>", CtsSubmitResult.InvalidResponseCode, false)]
    [InlineData(HttpStatusCode.OK, "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><TransferDataHexResponse xmlns=\"http://www.defra.gov.uk\"><TransferDataHexResult>not base64!</TransferDataHexResult></TransferDataHexResponse></soap:Body></soap:Envelope>", CtsSubmitResult.InvalidResponseCode, false)]
    [InlineData(HttpStatusCode.InternalServerError, "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Boom</faultstring></soap:Fault></soap:Body></soap:Envelope>", CtsSubmitResult.FaultCode, true)]
    public void ParseResponse_ClassifiesTransportAndProtocolFailures(HttpStatusCode status, string body, string expectedCode, bool expectedTransient)
    {
        var result = CtsWebClient.ParseResponse(status, body);

        Assert.False(result.IsAccepted);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedTransient, result.IsTransient);
    }

    [Fact]
    public async Task GetBirthResultsAsync_SendsAGetResultsRequestForTheReceiptWithCredentials()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, BirthResults("txn-1", "<Accepted><Accept RowNum=\"1\"/></Accepted>"));
        var client = CreateClient(handler);

        await client.GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        var transfer = XElement.Parse(Assert.Single(handler.Bodies)).Descendants(CtsWebClient.DefraNamespace + "TransferDataHex").Single();
        Assert.Equal(CtsGetResultsRequestBuilder.BirthResultsOperation, transfer.Element(CtsWebClient.DefraNamespace + "type")?.Value);

        var getResults = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(transfer.Element(CtsWebClient.DefraNamespace + "data")!.Value)));
        var ns = CtsGetResultsRequestBuilder.Namespace;
        Assert.Equal(ns + "GetResults", getResults.Name);
        Assert.Equal("cts-user", getResults.Element(ns + "Authentication")?.Element("CTS_OL_User")?.Attribute("Usr")?.Value);
        Assert.Equal("4242", getResults.Element(ns + "Receipt")?.Attribute("Num")?.Value);
    }

    [Fact]
    public async Task GetBirthResultsAsync_ReturnsTheAcceptedAndRejectedRows()
    {
        var rows = "<Accepted><Accept RowNum=\"1\"/><Accept RowNum=\"3\"/></Accepted>"
            + "<Rejected><Reject><Birth RowNum=\"2\" Etg=\"UK123456100002\" Brd=\"ZZ\"/><Causes>"
            + "<Cause Code=\"CTWS014\" Desc=\"Invalid Breed\" Sev=\"e\" Field=\"Brd\"/>"
            + "<Cause Code=\"CTWS003\" Desc=\"Missing Ear Tag\" Sev=\"e\" Field=\"Etg\"/>"
            + "</Causes></Reject></Rejected>";
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, BirthResults("txn-1", rows));

        var results = await CreateClient(handler).GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        Assert.Equal(CtsBirthResultsState.Ready, results.State);
        Assert.Equal("txn-1", results.TxnId);
        Assert.Equal([1, 3], results.AcceptedRows);
        var rejected = Assert.Single(results.RejectedRows);
        Assert.Equal(2, rejected.RowNum);
        Assert.Equal("UK123456100002", rejected.EarTag);
        Assert.Equal(
            [new CtsCause("CTWS014", "Invalid Breed", "e", "Brd"), new CtsCause("CTWS003", "Missing Ear Tag", "e", "Etg")],
            rejected.Causes);
    }

    [Fact]
    public async Task GetBirthResultsAsync_WhenCtsHasNotFinished_ReturnsNotReadyWithoutRetrying()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, SystemException(CtsBirthResults.NotReadyExNum, "Results not yet available"));

        var results = await CreateClient(handler).GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        Assert.Equal(CtsBirthResultsState.NotReady, results.State);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetBirthResultsAsync_WhenTheReceiptIsUnknown_ReturnsAPermanentFailure()
    {
        var handler = new CtsStubHandler().Respond(HttpStatusCode.OK, SystemException(CtsBirthResults.ReceiptNotFoundExNum, "Receipt not found"));

        var results = await CreateClient(handler).GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        Assert.Equal(CtsBirthResultsState.Failed, results.State);
        Assert.Equal(CtsBirthResults.ReceiptNotFoundExNum, results.ErrorCode);
        Assert.False(results.IsTransient);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetBirthResultsAsync_WhenCtsStaysUnavailable_ReturnsATransientFailureAfterMaxAttempts()
    {
        var handler = new CtsStubHandler()
            .Respond(HttpStatusCode.OK, SystemException(CtsSubmitResult.ServiceUnavailableExNum, "Service unavailable"))
            .Fail()
            .Respond(HttpStatusCode.ServiceUnavailable, "<busy/>");

        var results = await CreateClient(handler).GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        Assert.Equal(CtsBirthResultsState.Failed, results.State);
        Assert.True(results.IsTransient);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetBirthResultsAsync_WhenCtsIsBrieflyUnavailable_RetriesAndReturnsTheResults()
    {
        var handler = new CtsStubHandler()
            .Fail()
            .Respond(HttpStatusCode.OK, BirthResults("txn-1", "<Accepted><Accept RowNum=\"1\"/></Accepted>"));

        var results = await CreateClient(handler).GetBirthResultsAsync(4242, TestContext.Current.CancellationToken);

        Assert.Equal(CtsBirthResultsState.Ready, results.State);
        Assert.Equal([1], results.AcceptedRows);
    }

    [Fact]
    public void ParseBirthResultsResponse_WithNoRowsYet_ReturnsAnEmptyReadyResult()
    {
        var results = CtsWebClient.ParseBirthResultsResponse(HttpStatusCode.OK, BirthResults("txn-1", string.Empty));

        Assert.Equal(CtsBirthResultsState.Ready, results.State);
        Assert.Empty(results.AcceptedRows);
        Assert.Empty(results.RejectedRows);
    }

    [Theory]
    [InlineData("<MsgReceipt xmlns=\"http://defra.bcms.ctws/asynchronous_receipt\"><Receipt Num=\"1\"/></MsgReceipt>")]
    [InlineData("<BirthResults xmlns=\"http://defra.bcms.ctws/register_births_request_results\"/>")]
    [InlineData("<BirthResults xmlns=\"http://defra.bcms.ctws/register_births_request_results\"><Results TxnId=\"t\"><Accepted><Accept RowNum=\"x\"/></Accepted></Results></BirthResults>")]
    [InlineData("<BirthResults xmlns=\"http://defra.bcms.ctws/register_births_request_results\"><Results TxnId=\"t\"><Rejected><Reject><Causes/></Reject></Rejected></Results></BirthResults>")]
    public void ParseBirthResultsResponse_WhenTheResultIsNotValidBirthResults_ReturnsAnInvalidResponse(string inner)
    {
        var results = CtsWebClient.ParseBirthResultsResponse(HttpStatusCode.OK, Wrap(inner));

        Assert.Equal(CtsBirthResultsState.Failed, results.State);
        Assert.Equal(CtsSubmitResult.InvalidResponseCode, results.ErrorCode);
        Assert.False(results.IsTransient);
    }

    [Fact]
    public void ParseBirthResultsResponse_WhenTheTransportFails_ReturnsTheFailure()
    {
        var results = CtsWebClient.ParseBirthResultsResponse(HttpStatusCode.InternalServerError, "<html>down</html>");

        Assert.Equal(CtsBirthResultsState.Failed, results.State);
        Assert.Equal(CtsSubmitResult.UnavailableCode, results.ErrorCode);
        Assert.True(results.IsTransient);
    }

    internal static string BirthResults(string txnId, string rows) => Wrap(
        $"""<BirthResults xmlns="http://defra.bcms.ctws/register_births_request_results" SchemaVersion="1.0" ProgramName="CTS Webservices" ProgramVersion="1h" ResponseTimeStamp="2026-11-18T10:00:00Z"><Results TxnId="{txnId}">{rows}</Results></BirthResults>""");

    internal static string Receipt(long number) => Wrap(
        $"""<MsgReceipt xmlns="http://defra.bcms.ctws/asynchronous_receipt" SchemaVersion="1.0" ProgramName="CTS Webservices" ProgramVersion="1h" ResponseTimeStamp="2026-11-16T10:00:00Z"><Receipt Num="{number}"/></MsgReceipt>""");

    internal static string SystemException(string exNum, string exMsg) => Wrap($"""<SystemException ExNum="{exNum}" ExMsg="{exMsg}"/>""");

    private static string Wrap(string inner)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + inner));
        return $"""<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TransferDataHexResponse xmlns="http://www.defra.gov.uk"><TransferDataHexResult>{data}</TransferDataHexResult></TransferDataHexResponse></soap:Body></soap:Envelope>""";
    }

    private static string Payload() => CtsBirthRequestBuilder.Build(CtsBirthRequestBuilderTests.Bundle(1), DateTimeOffset.UtcNow);

#pragma warning disable CA5351, S4790
    private static string Md5(string value) => Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(value)));
#pragma warning restore CA5351, S4790

    private CtsWebClient CreateClient(CtsStubHandler handler) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), NullLogger<CtsWebClient>.Instance);

    internal sealed class CtsStubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public CtsStubHandler Respond(HttpStatusCode statusCode, string body)
        {
            responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "text/xml") }));
            return this;
        }

        public CtsStubHandler Fail()
        {
            responses.Enqueue(_ => throw new HttpRequestException("Connection refused"));
            return this;
        }

        public CtsStubHandler Hang()
        {
            responses.Enqueue(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return await responses.Dequeue()(cancellationToken);
        }
    }
}
