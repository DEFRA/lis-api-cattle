// <copyright file="StubHttpMessageHandler.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Upstream;

using System.Net;
using System.Text;

/// <summary>
/// Records outbound requests and answers each one from a queue of canned responses.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Gets the request bodies, read when each request is sent (null when a request has no content).
    /// </summary>
    public List<string?> RequestBodies { get; } = [];

    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string? json = null)
    {
        responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        });

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        if (responses.Count == 0)
        {
            throw new InvalidOperationException($"No stub response queued for {request.Method} {request.RequestUri}");
        }

        return responses.Dequeue()(request);
    }
}
