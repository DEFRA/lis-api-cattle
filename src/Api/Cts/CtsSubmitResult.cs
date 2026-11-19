// <copyright file="CtsSubmitResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>
/// The outcome of sending a request to CTS: a receipt number, or the error CTS (or the transport) returned.
/// </summary>
/// <param name="ReceiptNumber">The CTS receipt number when CTS accepted the request.</param>
/// <param name="ErrorCode">The CTS <c>ExNum</c>, or a local code for a transport failure.</param>
/// <param name="ErrorText">The CTS <c>ExMsg</c>, or a description of the transport failure.</param>
/// <param name="IsTransient">Whether trying again later could succeed (CTS unavailable, timeout, 5xx).</param>
public sealed record CtsSubmitResult(long? ReceiptNumber, string? ErrorCode, string? ErrorText, bool IsTransient)
{
    /// <summary>A transport failure: CTS could not be reached or did not answer in time.</summary>
    public const string UnavailableCode = "CTS_UNAVAILABLE";

    /// <summary>CTS answered with a SOAP fault.</summary>
    public const string FaultCode = "CTS_FAULT";

    /// <summary>CTS answered with something that is not a TransferDataHex response.</summary>
    public const string InvalidResponseCode = "CTS_INVALID_RESPONSE";

    /// <summary>CTS's "Request rejected (service unavailable)" exception number.</summary>
    public const string ServiceUnavailableExNum = "CTWS809";

    /// <summary>CTS's "Request rejected (submitted already)" exception number.</summary>
    public const string AlreadySubmittedExNum = "CTWS807";

    /// <summary>Gets a value indicating whether CTS accepted the request and returned a receipt.</summary>
    public bool IsAccepted => ReceiptNumber.HasValue;

    /// <summary>Creates an accepted result.</summary>
    /// <param name="receiptNumber">The CTS receipt number.</param>
    /// <returns>The result.</returns>
    public static CtsSubmitResult Accepted(long receiptNumber) => new(receiptNumber, null, null, false);

    /// <summary>Creates a failed result.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorText">The error text.</param>
    /// <param name="isTransient">Whether trying again later could succeed.</param>
    /// <returns>The result.</returns>
    public static CtsSubmitResult Failed(string errorCode, string errorText, bool isTransient) => new(null, errorCode, errorText, isTransient);
}
