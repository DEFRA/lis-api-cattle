// <copyright file="RegistrationBundleMapper.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using Defra.Lis.Entities;

/// <summary>
/// Unpacks a queued registration bundle into a submission and its animals.
/// </summary>
public static class RegistrationBundleMapper
{
    /// <summary>The submitter recorded when the caller did not name one.</summary>
    public const string DefaultSubmittedBy = "BE4FE";

    /// <summary>
    /// Creates the submission, with the ID the API gave the bundle and the status <c>submitted</c>.
    /// </summary>
    /// <param name="message">The queued bundle.</param>
    /// <returns>The submission, not yet saved.</returns>
    public static Submission ToSubmission(RegistrationBundleMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var bundle = message.Bundle ?? throw new ArgumentException("The message has no bundle.", nameof(message));

        var submittedBy = string.IsNullOrWhiteSpace(bundle.SubmittedBy) ? DefaultSubmittedBy : bundle.SubmittedBy;
        var submission = new Submission(
            id: message.SubmissionId,
            clientReference: bundle.ClientReference,
            countyParishHolding: bundle.Holding.Cph,
            submittedBy: submittedBy,
            status: Statuses.Submitted);
        submission.RecordCorrelationId(message.CorrelationId);

        foreach (var animal in bundle.Animals)
        {
            submission.AddAnimal(
                earTag: animal.EarTag,
                status: Statuses.Submitted,
                dateBirth: animal.DateOfBirth,
                sex: animal.Sex,
                breed: animal.Breed,
                damType: animal.Dam?.Type,
                damGeneticEarTag: animal.Dam?.GeneticDamEarTag,
                damSurrogateEarTag: string.IsNullOrWhiteSpace(animal.Dam?.SurrogateDamEarTag) ? null : animal.Dam.SurrogateDamEarTag,
                sireEarTag: string.IsNullOrWhiteSpace(animal.Sire?.EarTag) ? null : animal.Sire.EarTag,
                sireName: animal.Sire?.Name);
        }

        return submission;
    }
}
