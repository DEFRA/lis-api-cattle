// <copyright file="HoldingSearchRequestValidator.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.Text.RegularExpressions;
using Defra.Lis.Api.Models.Requests;
using FluentValidation;

/// <summary>
/// Validator for holding search query parameters.
/// </summary>
public partial class HoldingSearchRequestValidator : AbstractValidator<HoldingSearchRequest>
{
    private static readonly string[] AllowedOrderFields =
    [
        "cph",
        "identifier",
        "name",
        "holdingType",
        "startDate",
        "endDate"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="HoldingSearchRequestValidator"/> class.
    /// </summary>
    public HoldingSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.")
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search cannot exceed 200 characters.")
            .Matches(SearchPattern())
            .WithMessage("Search contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Search));

        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrEmpty(sort) ||
                          sort.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                          sort.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Sort must be either 'asc' or 'desc'.")
            .When(x => !string.IsNullOrEmpty(x.Sort));

        RuleFor(x => x.Order)
            .Must(order => string.IsNullOrEmpty(order) ||
                          AllowedOrderFields.Contains(order, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Order must be one of: cph, identifier, name, holdingType, startDate, endDate.")
            .When(x => !string.IsNullOrEmpty(x.Order));
    }

    [GeneratedRegex(@"^[\p{L}\p{N}\s/'@.+(),&-]*$")]
    private static partial Regex SearchPattern();
}
