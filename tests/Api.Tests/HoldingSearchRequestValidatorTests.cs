// <copyright file="HoldingSearchRequestValidatorTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Validation;

public class HoldingSearchRequestValidatorTests
{
    private readonly HoldingSearchRequestValidator validator = new();

    [Fact]
    public void Validator_WhenRequestIsEmpty_IsValid()
    {
        var request = new HoldingSearchRequest();
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Validator_WhenPageIsPositive_IsValid(int page)
    {
        var request = new HoldingSearchRequest(Page: page);
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validator_WhenPageIsLessThanOne_IsInvalid(int page)
    {
        var request = new HoldingSearchRequest(Page: page);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.Page) && e.ErrorMessage.Contains("greater than or equal to 1"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validator_WhenPageSizeIsWithinRange_IsValid(int pageSize)
    {
        var request = new HoldingSearchRequest(PageSize: pageSize);
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Validator_WhenPageSizeIsOutOfRange_IsInvalid(int pageSize)
    {
        var request = new HoldingSearchRequest(PageSize: pageSize);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.PageSize) && e.ErrorMessage.Contains("between 1 and 100"));
    }

    [Theory]
    [InlineData("Farm")]
    [InlineData("22/001/0001")]
    [InlineData("Oakfield-Farm (North) & Sons Ltd.")]
    [InlineData("farmer@example.com")]
    [InlineData("St. John's + Field, No. 1")]
    public void Validator_WhenSearchContainsValidCharacters_IsValid(string search)
    {
        var request = new HoldingSearchRequest(Search: search);
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_WhenSearchExceeds200Characters_IsInvalid()
    {
        var longSearch = new string('A', 201);
        var request = new HoldingSearchRequest(Search: longSearch);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.Search) && e.ErrorMessage.Contains("cannot exceed 200 characters"));
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("Farm; DROP TABLE")]
    [InlineData("Farm$")]
    [InlineData("Farm*")]
    [InlineData("Farm#")]
    public void Validator_WhenSearchContainsDisallowedCharacters_IsInvalid(string search)
    {
        var request = new HoldingSearchRequest(Search: search);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.Search) && e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    [InlineData("ASC")]
    [InlineData("DESC")]
    [InlineData("Asc")]
    public void Validator_WhenSortIsValid_IsValid(string sort)
    {
        var request = new HoldingSearchRequest(Sort: sort);
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("ascending")]
    [InlineData("descending")]
    [InlineData("invalid")]
    [InlineData("123")]
    public void Validator_WhenSortIsInvalid_IsInvalid(string sort)
    {
        var request = new HoldingSearchRequest(Sort: sort);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.Sort) && e.ErrorMessage.Contains("either 'asc' or 'desc'"));
    }

    [Theory]
    [InlineData("cph")]
    [InlineData("identifier")]
    [InlineData("name")]
    [InlineData("holdingType")]
    [InlineData("startDate")]
    [InlineData("endDate")]
    [InlineData("CPH")]
    [InlineData("Identifier")]
    [InlineData("Name")]
    [InlineData("HOLDINGTYPE")]
    [InlineData("StartDate")]
    [InlineData("EndDate")]
    public void Validator_WhenOrderIsValid_IsValid(string order)
    {
        var request = new HoldingSearchRequest(Order: order);
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("address")]
    [InlineData("invalid")]
    [InlineData("123")]
    public void Validator_WhenOrderIsInvalid_IsInvalid(string order)
    {
        var request = new HoldingSearchRequest(Order: order);
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(HoldingSearchRequest.Order) && e.ErrorMessage.Contains("Order must be one of"));
    }
}
