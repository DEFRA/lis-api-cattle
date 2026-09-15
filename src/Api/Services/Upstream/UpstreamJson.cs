// <copyright file="UpstreamJson.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services.Upstream;

using System.Text.Json;

/// <summary>
/// Serializer options for upstream payloads, which are camelCase (the SDK defaults to snake_case).
/// </summary>
public static class UpstreamJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
