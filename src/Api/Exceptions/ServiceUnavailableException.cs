// <copyright file="ServiceUnavailableException.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Exceptions;

/// <summary>
/// Raised when an upstream service reports that it is temporarily unavailable (HTTP 503), so the caller can retry
/// later. Mapped to 503 rather than 500, which signals an outright failure.
/// </summary>
public sealed class ServiceUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The upstream failure.</param>
    public ServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
