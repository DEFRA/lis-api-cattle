// <copyright file="ValidationFilter.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using FluentValidation;
using Microsoft.AspNetCore.Http;

/// <summary>
/// An endpoint filter that validates request arguments of type <typeparamref name="T"/> using FluentValidation.
/// </summary>
/// <typeparam name="T">The type of the request object to validate.</typeparam>
public class ValidationFilter<T>(IValidator<T> validator)
    : IEndpointFilter
    where T : class
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var arg = context.Arguments.OfType<T>().FirstOrDefault();

        if (arg == null)
        {
            return Results.BadRequest("Invalid request.");
        }

        var validationResult = await validator.ValidateAsync(arg, context.HttpContext.RequestAborted);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        return await next(context);
    }
}
