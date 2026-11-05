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
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Gets the request bodies, read when each request is sent (null when a request has no content).
    /// </summary>
    public List<string?> RequestBodies { get; } = [];

    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string? json = null)
    {
        responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        }));

        return this;
    }

    /// <summary>
    /// Fails the next request with <paramref name="exception"/>, as the transport does when the server cannot be reached.
    /// </summary>
    /// <param name="exception">The exception the request fails with.</param>
    /// <returns>This handler, to queue further responses.</returns>
    public StubHttpMessageHandler FailWith(Exception exception)
    {
        responses.Enqueue(_ => throw exception);

        return this;
    }

    /// <summary>
    /// Holds the next request open until it is cancelled, as a hung upstream does.
    /// </summary>
    /// <param name="received">Runs once the request reaches the handler, so a test can cancel it at that point.</param>
    /// <returns>This handler, to queue further responses.</returns>
    public StubHttpMessageHandler Hang(Action? received = null)
    {
        responses.Enqueue(async cancellationToken =>
        {
            received?.Invoke();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable: the delay only ends by cancellation.");
        });

        return this;
    }

    /// <summary>
    /// Answers the next request with 200 headers, then a body that never finishes until the request is cancelled.
    /// </summary>
    /// <returns>This handler, to queue further responses.</returns>
    public StubHttpMessageHandler StallBody()
    {
        responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledStream()),
        }));

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

        return await responses.Dequeue()(cancellationToken);
    }

    /// <summary>
    /// A response body that sends nothing until the read is cancelled.
    /// </summary>
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
