// <copyright file="CtsBirthRequestBuilderTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Cts;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Defra.Lis.Api.Cts;
using Defra.Lis.Entities;

public class CtsBirthRequestBuilderTests
{
    private const string DthUser = "dth";
    private const string DthSecretValue = "dth-secret";
    private const string CtsOnlineUser = "cts-user";
    private const string CtsOnlineSecretValue = "cts-secret";

    private static readonly XNamespace Ns = CtsBirthRequestBuilder.Namespace;

    private static readonly CtsWebOptions Options = new()
    {
        Url = "http://cts.test/cts_ws/DefraDataTransferPublicNWSE.asmx",
        ServiceName = "TEST",
        DthUsername = DthUser,
        DthPassword = DthSecretValue,
        CtsOnlineUsername = CtsOnlineUser,
        CtsOnlinePassword = CtsOnlineSecretValue,
    };

    [Fact]
    public void Build_OneAnimal_ProducesARequestThatValidatesAgainstTheCtsSchema()
    {
        var submission = Bundle(1);

        var payload = CtsBirthRequestBuilder.Build(submission, DateTimeOffset.Parse("2026-11-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        AssertValidAgainstSchema(CtsWebClient.AddAuthentication(payload, Options));
    }

    [Fact]
    public void Build_ManyAnimalsWithAndWithoutOptionalParents_ValidatesAgainstTheCtsSchema()
    {
        var submission = Bundle(3);
        submission.AddAnimal("UK 12 3456 100099", Statuses.Submitted, new DateOnly(2026, 3, 1), "m", "HF", "genetic", "UK 12 3456 000002", null, null, null);

        var payload = CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow);

        AssertValidAgainstSchema(CtsWebClient.AddAuthentication(payload, Options));
        Assert.Equal(4, XElement.Parse(payload).Descendants(Ns + "Birth").Count());
    }

    [Fact]
    public void Build_MapsTheBundleToCtsFieldsAndUsesTheHoldingAsBirthAndPostalLocation()
    {
        var submission = Bundle(1);

        var birth = XElement.Parse(CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow)).Descendants(Ns + "Birth").Single();

        Assert.Equal("1", birth.Attribute("RowNum")?.Value);
        Assert.Equal("UK123456100001", birth.Attribute("Etg")?.Value);
        Assert.Equal("2026-02-01", birth.Attribute("Dob")?.Value);
        Assert.Equal("AA", birth.Attribute("Brd")?.Value);
        Assert.Equal("f", birth.Attribute("Sex")?.Value);
        Assert.Equal("UK123456000002", birth.Attribute("GdEtg")?.Value);
        Assert.Equal("UK123456000003", birth.Attribute("SuEtg")?.Value);
        Assert.Equal("UK123456000010", birth.Attribute("SiEtg")?.Value);
        Assert.Equal("10/081/1234", birth.Attribute("BLoc")?.Value);
        Assert.Equal("10/081/1234", birth.Attribute("PLoc")?.Value);
    }

    [Fact]
    public void Build_OlderFormatParentTags_KeepTheirSpacesAndValidateAgainstTheCtsSchema()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        submission.AddAnimal("UK 12 3456 100001", Statuses.Submitted, new DateOnly(2026, 2, 1), "f", "HF", "surrogate", "UKAA2233 00009", "uk a1213  00003", "UK A1213 00003", null);

        var payload = CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow);
        var birth = XElement.Parse(payload).Descendants(Ns + "Birth").Single();

        AssertValidAgainstSchema(CtsWebClient.AddAuthentication(payload, Options));
        Assert.Equal("UK123456100001", birth.Attribute("Etg")?.Value);
        Assert.Equal("UKAA2233 00009", birth.Attribute("GdEtg")?.Value);
        Assert.Equal("UK A1213 00003", birth.Attribute("SuEtg")?.Value);
        Assert.Equal("UK A1213 00003", birth.Attribute("SiEtg")?.Value);
    }

    [Fact]
    public void Build_UsesTheSubmissionIdAsTheTransactionId()
    {
        var submission = Bundle(2);

        var births = XElement.Parse(CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow)).Element(Ns + "Births");

        Assert.Equal(submission.Id.ToString(), births?.Attribute("TxnId")?.Value);
        Assert.Equal(CtsBirthRequestBuilder.TxnIdFor(submission), births?.Attribute("TxnId")?.Value);
    }

    [Fact]
    public void Build_NeverContainsTheCtsOnlineCredentials()
    {
        var payload = CtsBirthRequestBuilder.Build(Bundle(1), DateTimeOffset.UtcNow);

        Assert.Null(XElement.Parse(payload).Element(Ns + "Authentication"));
        Assert.DoesNotContain(CtsOnlineSecretValue, payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NumbersRowsInTheSameOrderAsOrderedAnimals()
    {
        var submission = Bundle(3);

        var rows = XElement.Parse(CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow)).Descendants(Ns + "Birth")
            .Select(b => b.Attribute("Etg")!.Value)
            .ToList();

        Assert.Equal(CtsBirthRequestBuilder.OrderedAnimals(submission).Select(a => CtsFormat.EarTag(a.EarTag)), rows);
    }

    [Fact]
    public void Build_WhenAnAnimalHasNoGeneticDam_Throws()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        submission.AddAnimal("UK123456100001", Statuses.Submitted, new DateOnly(2026, 2, 1), "f", "AA", "surrogate", null, "UK123456000003", null, null);

        Assert.Throws<InvalidOperationException>(() => CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Build_WhenTheBundleHasNoAnimals_Throws()
    {
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");

        Assert.Throws<ArgumentException>(() => CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow));
    }

    internal static Submission Bundle(int animals)
    {
        var submission = new Submission(Guid.NewGuid(), "REF-925", "10/081/1234", "test user", Statuses.Submitted);
        for (var i = 1; i <= animals; i++)
        {
            submission.AddAnimal(
                earTag: $"UK 12 3456 10000{i}",
                status: Statuses.Submitted,
                dateBirth: new DateOnly(2026, 2, i),
                sex: i % 2 == 0 ? "male" : "female",
                breed: "AA",
                damType: "surrogate",
                damGeneticEarTag: "UK 12 3456 000002",
                damSurrogateEarTag: "UK 12 3456 000003",
                sireEarTag: "UK 12 3456 000010",
                sireName: "Example sire");
        }

        return submission;
    }

    private static void AssertValidAgainstSchema(string xml)
    {
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(null, Path.Combine(AppContext.BaseDirectory, "Cts", "Schemas", "register_births_request-V1-0.xsd"));

        var errors = new List<string>();
        XDocument.Parse(xml).Validate(schemas, (_, e) => errors.Add(e.Message));

        Assert.Empty(errors);
    }
}
