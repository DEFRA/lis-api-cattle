// <copyright file="CtsGetResultsRequestBuilder.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

using System.Globalization;
using System.Xml.Linq;

/// <summary>
/// Builds the CTSWeb <c>GetResults</c> request that asks for the validation results of an asynchronous submission.
/// </summary>
public static class CtsGetResultsRequestBuilder
{
    /// <summary>The CTSWeb operation (TransferDataHex <c>type</c>) for fetching birth validation results.</summary>
    public const string BirthResultsOperation = "Get_Register_Births_Validation_Results-V1-0";

    /// <summary>The namespace of the GetResults request (the spelling is CTS's own).</summary>
    public static readonly XNamespace Namespace = "http://defra.bcms.ctws/get_asynchronus_results";

    /// <summary>
    /// Builds the request for one receipt.
    /// </summary>
    /// <param name="receiptNumber">The receipt CTS returned when the bundle was submitted.</param>
    /// <param name="requestTimeStamp">The request time stamp.</param>
    /// <returns>The request XML, without the <c>Authentication</c> element.</returns>
    public static string Build(long receiptNumber, DateTimeOffset requestTimeStamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(receiptNumber);

        var document = new XElement(
            Namespace + "GetResults",
            new XAttribute("SchemaVersion", "1.0"),
            new XAttribute("ProgramName", CtsBirthRequestBuilder.ProgramName),
            new XAttribute("ProgramVersion", CtsBirthRequestBuilder.ProgramVersion),
            new XAttribute("RequestTimeStamp", requestTimeStamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
            new XElement(Namespace + "Receipt", new XAttribute("Num", receiptNumber.ToString(CultureInfo.InvariantCulture))));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}
