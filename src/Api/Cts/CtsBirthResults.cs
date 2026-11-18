// <copyright file="CtsBirthResults.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>
/// The outcome of asking CTS for a bundle's birth validation results. Rows missing from both lists have not been
/// processed yet and are returned by a later call.
/// </summary>
/// <param name="State">Whether results were returned.</param>
/// <param name="TxnId">The transaction ID CTS returned the results for (the submission ID).</param>
/// <param name="AcceptedRows">The row numbers CTS accepted.</param>
/// <param name="RejectedRows">The rows CTS rejected.</param>
/// <param name="ErrorCode">The error code, when the call did not return results.</param>
/// <param name="ErrorText">The error text, when the call did not return results.</param>
/// <param name="IsTransient">Whether a failed call could succeed if tried again.</param>
public sealed record CtsBirthResults(
    CtsBirthResultsState State,
    string? TxnId,
    IReadOnlyList<int> AcceptedRows,
    IReadOnlyList<CtsRejectedBirth> RejectedRows,
    string? ErrorCode,
    string? ErrorText,
    bool IsTransient)
{
    /// <summary>CTS's "results not yet available" exception number.</summary>
    public const string NotReadyExNum = "CTWS806";

    /// <summary>CTS's "receipt not found" exception number.</summary>
    public const string ReceiptNotFoundExNum = "CTWS002";

    /// <summary>Creates a result with the rows CTS has processed.</summary>
    /// <param name="txnId">The transaction ID CTS returned.</param>
    /// <param name="accepted">The accepted row numbers.</param>
    /// <param name="rejected">The rejected rows.</param>
    /// <returns>The result.</returns>
    public static CtsBirthResults Ready(string? txnId, IReadOnlyList<int> accepted, IReadOnlyList<CtsRejectedBirth> rejected) =>
        new(CtsBirthResultsState.Ready, txnId, accepted, rejected, null, null, false);

    /// <summary>Creates a result for a bundle CTS has not finished validating.</summary>
    /// <param name="errorText">The CTS message.</param>
    /// <returns>The result.</returns>
    public static CtsBirthResults NotReady(string errorText) =>
        new(CtsBirthResultsState.NotReady, null, [], [], NotReadyExNum, errorText, false);

    /// <summary>Creates a failed result.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorText">The error text.</param>
    /// <param name="isTransient">Whether trying again later could succeed.</param>
    /// <returns>The result.</returns>
    public static CtsBirthResults Failed(string errorCode, string errorText, bool isTransient) =>
        new(CtsBirthResultsState.Failed, null, [], [], errorCode, errorText, isTransient);
}
