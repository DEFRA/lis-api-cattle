// <copyright file="ApiExceptionHandler.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Exceptions;

using Defra.Lis.Core.Exceptions;
using Defra.Lis.Core.Middleware.Headers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;

public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, type) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found", "https://httpstatuses.com/404"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", "https://httpstatuses.com/409"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Bad Request", "https://httpstatuses.com/400"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "https://httpstatuses.com/400"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "https://httpstatuses.com/403"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "https://httpstatuses.com/500"),
        };

        // Put useful values into the Serilog LogContext (works with Enrich.FromLogContext()).
        var correlationId = httpContext.Request.Headers[RequestHeaderNames.CorrelationId].ToString();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", httpContext.TraceIdentifier))
        using (LogContext.PushProperty("Path", httpContext.Request.Path.Value))
        using (LogContext.PushProperty("StatusCode", statusCode))
        {
            if (statusCode >= 500)
            {
                LogUnhandledExceptionWhileProcessingRequestMethodPath(httpContext.Request.Method, httpContext.Request.Path, exception);
            }
            else
            {
                LogRequestFailedWithStatusCodeTitleForMethodPath(statusCode, title, httpContext.Request.Method, httpContext.Request.Path, exception);
            }
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true; // exception handled
    }
}
