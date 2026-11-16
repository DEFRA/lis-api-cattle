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

    private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
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
        var delay = TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds);
        var attempts = Math.Max(1, settings.MaxAttempts);

        CtsSubmitResult result = CtsSubmitResult.Failed(CtsSubmitResult.UnavailableCode, "CTS was not called.", true);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            result = await SendOnceAsync(settings, envelope, cancellationToken);
            if (result.IsAccepted || !result.IsTransient)
            {
                return result;
            }

            LogCtsAttemptFailed(attempt, attempts, result.ErrorCode, result.ErrorText);
            if (attempt < attempts && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }

        return result;
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
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(XmlDeclaration + request));
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
        return XmlDeclaration + envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Reads a TransferDataHex response: a <c>MsgReceipt</c> with a receipt number, or a <c>SystemException</c>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The outcome.</returns>
    internal static CtsSubmitResult ParseResponse(HttpStatusCode statusCode, string body)
    {
        var code = (int)statusCode;
        XElement envelope;
        try
        {
            envelope = XElement.Parse(body);
        }
        catch (XmlException)
        {
            return CtsSubmitResult.Failed(
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
            return CtsSubmitResult.Failed(CtsSubmitResult.FaultCode, $"{faultCode}: {faultString}", !isClientFault);
        }

        if (statusCode != HttpStatusCode.OK)
        {
            return CtsSubmitResult.Failed(
                code >= 500 ? CtsSubmitResult.UnavailableCode : CtsSubmitResult.InvalidResponseCode,
                $"CTS returned HTTP {code}.",
                code >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests);
        }

        var result = envelope.Descendants(DefraNamespace + "TransferDataHexResult").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(result))
        {
            return CtsSubmitResult.Failed(CtsSubmitResult.InvalidResponseCode, "CTS returned no TransferDataHexResult.", false);
        }

        XElement inner;
        try
        {
            inner = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(result)).TrimStart('﻿'));
        }
        catch (Exception ex) when (ex is FormatException or XmlException)
        {
            return CtsSubmitResult.Failed(CtsSubmitResult.InvalidResponseCode, "CTS returned a TransferDataHexResult that could not be decoded.", false);
        }

        if (inner.Name.LocalName == "SystemException")
        {
            var exNum = inner.Attribute("ExNum")?.Value ?? CtsSubmitResult.InvalidResponseCode;
            var exMsg = inner.Attribute("ExMsg")?.Value ?? "CTS rejected the request.";
            return CtsSubmitResult.Failed(exNum, exMsg, exNum == CtsSubmitResult.ServiceUnavailableExNum);
        }

        var receipt = inner.Element(ReceiptNamespace + "Receipt")?.Attribute("Num")?.Value;
        if (inner.Name == ReceiptNamespace + "MsgReceipt"
            && long.TryParse(receipt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var receiptNumber))
        {
            return CtsSubmitResult.Accepted(receiptNumber);
        }

        return CtsSubmitResult.Failed(CtsSubmitResult.InvalidResponseCode, $"CTS returned an unexpected {inner.Name.LocalName} result.", false);
    }

    // MD5 matches the real service's PasswordHelper. It is required for wire compatibility with this legacy
    // protocol and is not used as a security control.
#pragma warning disable CA5351, S4790
    private static string HashPassword(string password) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(password)));
#pragma warning restore CA5351, S4790

    private async Task<CtsSubmitResult> SendOnceAsync(CtsWebOptions settings, string envelope, CancellationToken cancellationToken)
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
            return ParseResponse(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CtsSubmitResult.Failed(CtsSubmitResult.UnavailableCode, $"CTS did not answer within {settings.TimeoutMilliseconds} ms.", true);
        }
        catch (HttpRequestException ex)
        {
            LogCtsCouldNotBeReached(ex);
            return CtsSubmitResult.Failed(CtsSubmitResult.UnavailableCode, "CTS could not be reached.", true);
        }
    }
}
