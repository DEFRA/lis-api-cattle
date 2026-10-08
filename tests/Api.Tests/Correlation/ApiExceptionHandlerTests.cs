// <copyright file="ApiExceptionHandlerTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Correlation;

using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task ProblemDetails_CarryTheCorrelationId()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Headers[TraceHeaders.CdpRequestId] = "corr-598";
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new NotFoundException("missing"), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("corr-598", context.Response.Headers[TraceHeaders.CdpRequestId].ToString());
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("corr-598", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-1", problem.RootElement.GetProperty("traceId").GetString());
    }
}
