// <copyright file="UpstreamErrors.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services.Upstream;

using System.Net;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;

/// <summary>
/// Helpers for interpreting failures raised by the strategies SDK REST client, which wraps the
/// <see cref="HttpRequestException"/> thrown for non-success status codes.
/// </summary>
public static class UpstreamErrors
{
    public static HttpStatusCode? StatusCode(RestResponseException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is HttpRequestException { StatusCode: { } statusCode }
            ? statusCode
            : null;
    }

    public static bool IsNotFound(RestResponseException exception) =>
        StatusCode(exception) == HttpStatusCode.NotFound;

    public static bool IsBadRequest(RestResponseException exception) =>
        StatusCode(exception) == HttpStatusCode.BadRequest;

    public static bool IsUnprocessableEntity(RestResponseException exception) =>
        StatusCode(exception) == HttpStatusCode.UnprocessableEntity;

    public static bool IsConflict(RestResponseException exception) =>
        StatusCode(exception) == HttpStatusCode.Conflict;

    public static bool IsServiceUnavailable(RestResponseException exception) =>
        StatusCode(exception) == HttpStatusCode.ServiceUnavailable;

    /// <summary>
    /// Whether the upstream answered that it is temporarily unavailable: 503, or 504 from a gateway in front of it.
    /// </summary>
    /// <param name="exception">The response failure.</param>
    /// <returns><see langword="true"/> for 503 or 504.</returns>
    public static bool IsServiceUnavailableOrGatewayTimeout(RestResponseException exception) =>
        StatusCode(exception) is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    /// <summary>
    /// Whether a call failed without the upstream answering, and the caller did not cancel it. The SDK wraps every
    /// exception <c>HttpClient.SendAsync</c> throws in a <see cref="RestRequestException"/>: a transport failure
    /// (connection refused, DNS), the resilience pipeline's timeout or open circuit, or the HttpClient timeout.
    /// </summary>
    /// <param name="exception">The request failure.</param>
    /// <param name="cancellationToken">The caller's token; a cancelled call is not the upstream's fault.</param>
    /// <returns><see langword="true"/> when the upstream should be reported as unavailable.</returns>
    public static bool IsUnreachable(RestRequestException exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is not null && !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Whether an upstream call failed because the upstream is unavailable, so the API should answer 503: it could not
    /// be reached, timed out, has its circuit open (<see cref="IsUnreachable"/>), or answered 503 or 504.
    /// </summary>
    /// <param name="exception">The exception the call failed with.</param>
    /// <param name="cancellationToken">The caller's token; a cancelled call is not the upstream's fault.</param>
    /// <returns><see langword="true"/> when the upstream should be reported as unavailable.</returns>
    public static bool IsUnavailable(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        RestRequestException request => IsUnreachable(request, cancellationToken),
        RestResponseException response => IsServiceUnavailableOrGatewayTimeout(response),
        _ => false,
    };
}
