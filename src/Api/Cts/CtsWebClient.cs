// <copyright file="CtsWebClient.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Calls the CTS Web Services <c>TransferDataHex</c> SOAP operation (<c>DefraDataTransferPublicNWSE.asmx</c>).
/// The inner CTSWeb request is sent base64-encoded in the envelope's <c>data</c> element.
/// </summary>
public partial class CtsWebClient(
    HttpClient httpClient,
    IOptions<CtsWebOptions> options,
    ILogger<CtsWebClient> logger)
    : ICtsWebClient
{
    // XML namespace names, not addresses that are fetched.
#pragma warning disable S5332
    /// <summary>The SOAP envelope namespace.</summary>
    public static readonly XNamespace SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>The namespace of the TransferDataHex request and response.</summary>
    public static readonly XNamespace DefraNamespace = "http://www.defra.gov.uk";

    /// <summary>The namespace of the asynchronous receipt (MsgReceipt) response.</summary>
    public static readonly XNamespace ReceiptNamespace = "http://defra.bcms.ctws/asynchronous_receipt";

    /// <summary>The namespace of the birth validation results (BirthResults) response.</summary>
    public static readonly XNamespace BirthResultsNamespace = "http://defra.bcms.ctws/register_births_request_results";

    // CTSWeb is a legacy service, so both declarations copy the CTS proving-facility sender byte for byte: the SOAP
    // envelope uses single quotes and lower-case utf-8, the base64 data payload double quotes and upper-case UTF-8.
    private const string EnvelopeXmlDeclaration = "<?xml version='1.0' encoding='utf-8'?>";
    private const string PayloadXmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
    private const string SoapAction = "http://www.defra.gov.uk/TransferDataHex";
#pragma warning restore S5332

    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public async Task<CtsSubmitResult> SubmitAsync(string operation, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        // Validated here rather than on start-up, so the service can boot before the CTS secrets are configured.
        var settings = options.Value;
        var envelope = BuildEnvelope(settings, operation, AddAuthentication(payload, settings));
        return ToSubmitResult(await CallAsync(settings, envelope, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<CtsBirthResults> GetBirthResultsAsync(long receiptNumber, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var request = CtsGetResultsRequestBuilder.Build(receiptNumber, DateTimeOffset.UtcNow);
        var envelope = BuildEnvelope(settings, CtsGetResultsRequestBuilder.BirthResultsOperation, AddAuthentication(request, settings));
        return ToBirthResults(await CallAsync(settings, envelope, cancellationToken));
    }

    /// <summary>
    /// Adds the CTS Online credentials to a stored request. <c>CTS_OL_User</c> is declared in a schema with no target
    /// namespace, so it is written unqualified (<c>xmlns=""</c>), as the real client does.
    /// </summary>
    /// <param name="payload">The stored request XML.</param>
    /// <param name="settings">The CTS settings.</param>
    /// <returns>The request XML with its <c>Authentication</c> element.</returns>
    internal static string AddAuthentication(string payload, CtsWebOptions settings)
    {
        var root = XElement.Parse(payload);
        root.AddFirst(new XElement(
            root.Name.Namespace + "Authentication",
            new XElement(
                "CTS_OL_User",
                new XAttribute("Usr", settings.CtsOnlineUsername),
                new XAttribute("Pwd", settings.CtsOnlinePassword))));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Builds the TransferDataHex SOAP envelope around a CTSWeb request.
    /// </summary>
    /// <param name="settings">The CTS settings.</param>
    /// <param name="operation">The CTSWeb operation.</param>
    /// <param name="request">The CTSWeb request XML, including its credentials.</param>
    /// <returns>The SOAP envelope.</returns>
    internal static string BuildEnvelope(CtsWebOptions settings, string operation, string request)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(PayloadXmlDeclaration + request));
        var envelope = new XElement(
            SoapNamespace + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", SoapNamespace),
            new XElement(
                SoapNamespace + "Body",
                new XElement(
                    DefraNamespace + "TransferDataHex",
                    new XElement(DefraNamespace + "username", settings.DthUsername),
                    new XElement(DefraNamespace + "password", HashPassword(settings.DthPassword)),
                    new XElement(DefraNamespace + "serviceName", settings.ServiceName),
                    new XElement(DefraNamespace + "data", data),
                    new XElement(DefraNamespace + "type", operation))));
        return EnvelopeXmlDeclaration + envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Reads a TransferDataHex response to a Register request: a <c>MsgReceipt</c> with a receipt number, or a
    /// <c>SystemException</c>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The outcome.</returns>
    internal static CtsSubmitResult ParseResponse(HttpStatusCode statusCode, string body) =>
        ToSubmitResult(ReadEnvelope(statusCode, body));

    /// <summary>
    /// Reads a TransferDataHex response to a <c>Get_Register_Births_Validation_Results</c> request: a
    /// <c>BirthResults</c> document, or a <c>SystemException</c> (<c>CTWS806</c> while CTS is still validating).
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The outcome.</returns>
    internal static CtsBirthResults ParseBirthResultsResponse(HttpStatusCode statusCode, string body) =>
        ToBirthResults(ReadEnvelope(statusCode, body));

    private static CtsSubmitResult ToSubmitResult(CtsCallOutcome outcome)
    {
        if (outcome.Result is not { } inner)
        {
            return CtsSubmitResult.Failed(outcome.ErrorCode!, outcome.ErrorText!, outcome.IsTransient);
        }

        var receipt = inner.Element(ReceiptNamespace + "Receipt")?.Attribute("Num")?.Value;
        if (inner.Name == ReceiptNamespace + "MsgReceipt"
            && long.TryParse(receipt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var receiptNumber))
        {
            return CtsSubmitResult.Accepted(receiptNumber);
        }

        return CtsSubmitResult.Failed(CtsSubmitResult.InvalidResponseCode, $"CTS returned an unexpected {inner.Name.LocalName} result.", false);
    }

    private static CtsBirthResults ToBirthResults(CtsCallOutcome outcome)
    {
        if (outcome.Result is not { } inner)
        {
            return outcome.ErrorCode == CtsBirthResults.NotReadyExNum
                ? CtsBirthResults.NotReady(outcome.ErrorText!)
                : CtsBirthResults.Failed(outcome.ErrorCode!, outcome.ErrorText!, outcome.IsTransient);
        }

        var results = inner.Name == BirthResultsNamespace + "BirthResults"
            ? inner.Element(BirthResultsNamespace + "Results")
            : null;
        if (results == null)
        {
            return CtsBirthResults.Failed(CtsSubmitResult.InvalidResponseCode, $"CTS returned an unexpected {inner.Name.LocalName} result.", false);
        }

        var accepted = new List<int>();
        foreach (var accept in results.Elements(BirthResultsNamespace + "Accepted").Elements(BirthResultsNamespace + "Accept"))
        {
            if (!TryReadRowNum(accept, out var rowNum))
            {
                return InvalidRow();
            }

            accepted.Add(rowNum);
        }

        var rejected = new List<CtsRejectedBirth>();
        foreach (var reject in results.Elements(BirthResultsNamespace + "Rejected").Elements(BirthResultsNamespace + "Reject"))
        {
            var birth = reject.Element(BirthResultsNamespace + "Birth");
            if (birth == null || !TryReadRowNum(birth, out var rowNum))
            {
                return InvalidRow();
            }

            var causes = reject.Elements(BirthResultsNamespace + "Causes").Elements(BirthResultsNamespace + "Cause")
                .Select(c => new CtsCause(
                    c.Attribute("Code")?.Value ?? string.Empty,
                    c.Attribute("Desc")?.Value ?? string.Empty,
                    c.Attribute("Sev")?.Value,
                    c.Attribute("Field")?.Value))
                .ToList();
            rejected.Add(new CtsRejectedBirth(rowNum, birth.Attribute("Etg")?.Value, causes));
        }

        return CtsBirthResults.Ready(results.Attribute("TxnId")?.Value, accepted, rejected);

        static CtsBirthResults InvalidRow() =>
            CtsBirthResults.Failed(CtsSubmitResult.InvalidResponseCode, "CTS returned a result row without a valid RowNum.", false);
    }

    private static bool TryReadRowNum(XElement row, out int rowNum) =>
        int.TryParse(row.Attribute("RowNum")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowNum) && rowNum > 0;

    /// <summary>
    /// Reads the TransferDataHex envelope down to the decoded CTSWeb result, or the failure.
    /// </summary>
    private static CtsCallOutcome ReadEnvelope(HttpStatusCode statusCode, string body)
    {
        var code = (int)statusCode;
        XElement envelope;
        try
        {
            envelope = XElement.Parse(body);
        }
        catch (XmlException)
        {
            return CtsCallOutcome.Failed(
                code >= 500 ? CtsSubmitResult.UnavailableCode : CtsSubmitResult.InvalidResponseCode,
                $"CTS returned HTTP {code} with a body that is not XML.",
                code >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests);
        }

        var fault = envelope.Descendants(SoapNamespace + "Fault").FirstOrDefault();
        if (fault != null)
        {
            // soap:Client means our request (for example the DTH credentials) is wrong, so a retry cannot help.
            var faultCode = fault.Element("faultcode")?.Value ?? string.Empty;
            var faultString = fault.Element("faultstring")?.Value ?? "SOAP fault";
            var isClientFault = faultCode.EndsWith("Client", StringComparison.Ordinal);
            return CtsCallOutcome.Failed(CtsSubmitResult.FaultCode, $"{faultCode}: {faultString}", !isClientFault);
        }

        if (statusCode != HttpStatusCode.OK)
        {
            return CtsCallOutcome.Failed(
                code >= 500 ? CtsSubmitResult.UnavailableCode : CtsSubmitResult.InvalidResponseCode,
                $"CTS returned HTTP {code}.",
                code >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests);
        }

        var result = envelope.Descendants(DefraNamespace + "TransferDataHexResult").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(result))
        {
            return CtsCallOutcome.Failed(CtsSubmitResult.InvalidResponseCode, "CTS returned no TransferDataHexResult.", false);
        }

        XElement inner;
        try
        {
            inner = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(result)).TrimStart('\uFEFF'));
        }
        catch (Exception ex) when (ex is FormatException or XmlException)
        {
            return CtsCallOutcome.Failed(CtsSubmitResult.InvalidResponseCode, "CTS returned a TransferDataHexResult that could not be decoded.", false);
        }

        if (inner.Name.LocalName == "SystemException")
        {
            var exNum = inner.Attribute("ExNum")?.Value ?? CtsSubmitResult.InvalidResponseCode;
            var exMsg = inner.Attribute("ExMsg")?.Value ?? "CTS rejected the request.";
            return CtsCallOutcome.Failed(exNum, exMsg, exNum == CtsSubmitResult.ServiceUnavailableExNum);
        }

        return new CtsCallOutcome(inner, null, null, false);
    }

    // MD5 matches the real service's PasswordHelper. It is required for wire compatibility with this legacy
    // protocol and is not used as a security control.
#pragma warning disable CA5351, S4790
    private static string HashPassword(string password) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(password)));
#pragma warning restore CA5351, S4790

    /// <summary>
    /// Sends one envelope, trying again while the failure is transient, up to <see cref="CtsWebOptions.MaxAttempts"/> times.
    /// </summary>
    private async Task<CtsCallOutcome> CallAsync(CtsWebOptions settings, string envelope, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds);
        var attempts = Math.Max(1, settings.MaxAttempts);

        var outcome = CtsCallOutcome.Failed(CtsSubmitResult.UnavailableCode, "CTS was not called.", true);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            outcome = await SendOnceAsync(settings, envelope, cancellationToken);
            if (outcome.Result != null || !outcome.IsTransient)
            {
                return outcome;
            }

            LogCtsAttemptFailed(attempt, attempts, outcome.ErrorCode, outcome.ErrorText);
            if (attempt < attempts && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }

        return outcome;
    }

    private async Task<CtsCallOutcome> SendOnceAsync(CtsWebOptions settings, string envelope, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.TimeoutMilliseconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Url)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", $"\"{SoapAction}\"");

        // The worker has no inbound request to propagate headers from, so the correlation ID is added here.
        if (!string.IsNullOrWhiteSpace(CorrelationIdContext.Value))
        {
            request.Headers.TryAddWithoutValidation(TraceHeaders.CdpRequestId, CorrelationIdContext.Value);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return ReadEnvelope(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CtsCallOutcome.Failed(CtsSubmitResult.UnavailableCode, $"CTS did not answer within {settings.TimeoutMilliseconds} ms.", true);
        }
        catch (HttpRequestException ex)
        {
            LogCtsCouldNotBeReached(ex);
            return CtsCallOutcome.Failed(CtsSubmitResult.UnavailableCode, "CTS could not be reached.", true);
        }
    }

    /// <summary>One TransferDataHex call: the decoded CTSWeb result, or why there is none.</summary>
    private sealed record CtsCallOutcome(XElement? Result, string? ErrorCode, string? ErrorText, bool IsTransient)
    {
        public static CtsCallOutcome Failed(string errorCode, string errorText, bool isTransient) =>
            new(null, errorCode, errorText, isTransient);
    }
}
