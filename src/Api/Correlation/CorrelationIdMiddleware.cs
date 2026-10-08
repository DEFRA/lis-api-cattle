// <copyright file="CorrelationIdMiddleware.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Correlation;

using Defra.Lis.Api.Configurations;
using Serilog.Context;

/// <summary>
/// Requires a single <c>x-cdp-request-id</c> header on every endpoint not marked with
/// <see cref="IgnoreCorrelationIdCheck"/>, rejecting the request with 400 otherwise. The value is stored in
/// <see cref="CorrelationIdContext"/>, added to every log line as <c>CorrelationId</c> and echoed on the response.
/// </summary>
public sealed partial class CorrelationIdMiddleware(
    IProblemDetailsService problemDetailsService,
    ILogger<CorrelationIdMiddleware> logger)
    : IMiddleware
{
    /// <summary>The <c>code</c> extension on the 400 response when the header is missing.</summary>
    public const string MissingHeaderCode = "missing_header";

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<IgnoreCorrelationIdCheck>() is not null)
        {
            await next(context);
            return;
        }

        var values = context.Request.Headers[TraceHeaders.CdpRequestId];
        var correlationId = values.Count == 1 ? values.ToString() : null;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            LogMissingCorrelationId(context.Request.Method, context.Request.Path);
            await WriteMissingHeaderAsync(context);
            return;
        }

        CorrelationIdContext.Value = correlationId;
        context.Response.Headers[TraceHeaders.CdpRequestId] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private async Task WriteMissingHeaderAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Type = "https://httpstatuses.com/400",
                Detail = $"A single {TraceHeaders.CdpRequestId} header is required.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["code"] = MissingHeaderCode,
                    ["traceId"] = context.TraceIdentifier,
                },
            },
        });
    }
}
