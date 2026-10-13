// <copyright file="ValidationFilterTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Validation;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

public class ValidationFilterTests
{
    [Fact]
    public async Task InvokeAsync_WhenArgumentIsNull_ReturnsBadRequest()
    {
        var mockValidator = new Mock<IValidator<HoldingSearchRequest>>();
        var filter = new ValidationFilter<HoldingSearchRequest>(mockValidator.Object);

        var httpContext = new DefaultHttpContext();
        var invocationContext = new DefaultEndpointFilterInvocationContext(httpContext, "not a holding search request");

        var result = await filter.InvokeAsync(
            invocationContext,
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationFails_ReturnsValidationProblem()
    {
        var mockValidator = new Mock<IValidator<HoldingSearchRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<HoldingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Page", "Page must be greater than or equal to 1")]));

        var filter = new ValidationFilter<HoldingSearchRequest>(mockValidator.Object);

        var httpContext = new DefaultHttpContext();
        var request = new HoldingSearchRequest(Page: 0);
        var invocationContext = new DefaultEndpointFilterInvocationContext(httpContext, request);

        var result = await filter.InvokeAsync(
            invocationContext,
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.IsType<ProblemHttpResult>(result);
        var problem = (ProblemHttpResult)result!;
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationSucceeds_CallsNextDelegate()
    {
        var mockValidator = new Mock<IValidator<HoldingSearchRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<HoldingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var filter = new ValidationFilter<HoldingSearchRequest>(mockValidator.Object);

        var httpContext = new DefaultHttpContext();
        var request = new HoldingSearchRequest(Page: 1);
        var invocationContext = new DefaultEndpointFilterInvocationContext(httpContext, request);

        var nextCalled = false;
        var result = await filter.InvokeAsync(
            invocationContext,
            _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok("success"));
            });

        Assert.True(nextCalled);
        Assert.IsType<Ok<string>>(result);
    }
}
