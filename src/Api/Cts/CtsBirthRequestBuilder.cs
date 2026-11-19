// <copyright file="CtsBirthRequestBuilder.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

using System.Globalization;
using System.Xml.Linq;
using Defra.Lis.Entities;

/// <summary>
/// Builds the CTSWeb <c>Register_Births_Asynchronous</c> request (register_births_request-V1-0.xsd) for a bundle.
/// </summary>
/// <remarks>
/// The document is built without its <c>Authentication</c> element. That is the form stored in Postgres, so the
/// CTS Online password is never written to the database. <see cref="CtsWebClient"/> adds the element when it sends.
/// </remarks>
public static class CtsBirthRequestBuilder
{
    /// <summary>The CTSWeb operation (TransferDataHex <c>type</c>) for registering births.</summary>
    public const string Operation = "Register_Births_Asynchronous-V1-0";

    /// <summary>The program name sent in the request header.</summary>
    public const string ProgramName = "LIS Cattle API";

    /// <summary>The program version sent in the request header.</summary>
    public const string ProgramVersion = "1.0";

    /// <summary>The namespace of the register births request.</summary>
    public static readonly XNamespace Namespace = "http://defra.bcms.ctws/register_births_request";

    /// <summary>
    /// Builds the Birth request for every animal in the bundle, in a stable order, so that row <c>n</c> is always
    /// the same animal. The CTS transaction ID is the submission ID, so a resend is recognised as a duplicate.
    /// </summary>
    /// <param name="submission">The stored bundle.</param>
    /// <param name="requestTimeStamp">The request time stamp.</param>
    /// <returns>The request XML, without the <c>Authentication</c> element.</returns>
    public static string Build(Submission submission, DateTimeOffset requestTimeStamp)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (submission.Animals.Count == 0)
        {
            throw new ArgumentException("A CTS Birth request needs at least one animal.", nameof(submission));
        }

        // The postal location is the holding CPH, as agreed for LREG-925.
        var location = submission.CountyParishHolding;

        var births = new XElement(Namespace + "Births", new XAttribute("TxnId", TxnIdFor(submission)));
        var rowNumber = 0;

        foreach (var animal in OrderedAnimals(submission))
        {
            rowNumber++;
            var birth = new XElement(
                Namespace + "Birth",
                new XAttribute("RowNum", rowNumber.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("Etg", Required(CtsFormat.EarTag(animal.EarTag), "ear tag", animal)),
                new XAttribute("Dob", Required(animal.DateBirth, "date of birth", animal).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XAttribute("Brd", Required(animal.Breed, "breed", animal)),
                new XAttribute("Sex", Required(CtsFormat.Sex(animal.Sex), "sex", animal)),
                new XAttribute("GdEtg", Required(CtsFormat.EarTag(animal.DamGeneticEarTag), "genetic dam ear tag", animal)));

            AddOptional(birth, "SuEtg", CtsFormat.EarTag(animal.DamSurrogateEarTag));
            AddOptional(birth, "SiEtg", CtsFormat.EarTag(animal.SireEarTag));
            birth.Add(new XAttribute("BLoc", location), new XAttribute("PLoc", location));
            births.Add(birth);
        }

        var document = new XElement(
            Namespace + "RegBirths",
            new XAttribute("SchemaVersion", "1.0"),
            new XAttribute("ProgramName", ProgramName),
            new XAttribute("ProgramVersion", ProgramVersion),
            new XAttribute("RequestTimeStamp", requestTimeStamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
            births);

        return document.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// The CTS transaction ID for a bundle: its submission ID (36 characters, within the 40 CTS allows).
    /// </summary>
    /// <param name="submission">The bundle.</param>
    /// <returns>The transaction ID.</returns>
    public static string TxnIdFor(Submission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return submission.Id.ToString("D", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The animals in the order their rows are numbered, so that CTS validation results (part two) can be mapped
    /// back to animals by row number.
    /// </summary>
    /// <param name="submission">The bundle.</param>
    /// <returns>The animals in row order (row 1 first).</returns>
    public static IReadOnlyList<SubmissionAnimal> OrderedAnimals(Submission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return submission.Animals
            .OrderBy(a => CtsFormat.EarTag(a.EarTag), StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToList();
    }

    private static void AddOptional(XElement birth, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            birth.Add(new XAttribute(name, value));
        }
    }

    private static T Required<T>(T? value, string field, SubmissionAnimal animal)
        where T : struct =>
        value ?? throw new InvalidOperationException($"Animal {animal.Id} has no {field}, which a CTS Birth request requires.");

    private static string Required(string? value, string field, SubmissionAnimal animal) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Animal {animal.Id} has no valid {field}, which a CTS Birth request requires.")
            : value;
}
