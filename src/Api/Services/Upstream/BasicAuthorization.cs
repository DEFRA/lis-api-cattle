// <copyright file="BasicAuthorization.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services.Upstream;

using System.Text;

/// <summary>
/// Builds the HTTP Basic <c>Authorization</c> header value expected by the upstream fakes and services.
/// </summary>
public static class BasicAuthorization
{
    public const string HeaderName = "Authorization";

    public static string HeaderValue(string clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return $"Basic {credentials}";
    }
}
