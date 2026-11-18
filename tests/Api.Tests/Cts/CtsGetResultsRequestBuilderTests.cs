// <copyright file="CtsGetResultsRequestBuilderTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Cts;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Defra.Lis.Api.Cts;

public class CtsGetResultsRequestBuilderTests
{
    private const string CtsOnlinePasswordValue = "cts-secret";

    private static readonly CtsWebOptions Options = new()
    {
        CtsOnlineUsername = "cts-user",
        CtsOnlinePassword = CtsOnlinePasswordValue,
    };

    [Fact]
    public void Build_WithCredentials_ProducesARequestThatValidatesAgainstTheCtsSchema()
    {
        var payload = CtsGetResultsRequestBuilder.Build(4242, DateTimeOffset.Parse("2026-11-18T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var request = CtsWebClient.AddAuthentication(payload, Options);

        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(null, Path.Combine(AppContext.BaseDirectory, "Cts", "Schemas", "get_asynchronous_results-V1-0.xsd"));
        var errors = new List<string>();
        XDocument.Parse(request).Validate(schemas, (_, e) => errors.Add(e.Message));
        Assert.Empty(errors);
    }

    [Fact]
    public void Build_CarriesTheReceiptNumberAndTimeStamp()
    {
        var root = XElement.Parse(CtsGetResultsRequestBuilder.Build(4242, DateTimeOffset.Parse("2026-11-18T09:00:00+01:00", System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(CtsGetResultsRequestBuilder.Namespace + "GetResults", root.Name);
        Assert.Equal("4242", root.Element(CtsGetResultsRequestBuilder.Namespace + "Receipt")?.Attribute("Num")?.Value);
        Assert.Equal("2026-11-18T08:00:00Z", root.Attribute("RequestTimeStamp")?.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Build_WithoutAValidReceipt_Throws(long receiptNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CtsGetResultsRequestBuilder.Build(receiptNumber, DateTimeOffset.UtcNow));
    }
}
