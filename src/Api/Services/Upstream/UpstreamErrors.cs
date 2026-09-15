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
}
