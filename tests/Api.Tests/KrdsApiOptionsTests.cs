// <copyright file="KrdsApiOptionsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.ComponentModel.DataAnnotations;
using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Configuration;
using Xunit;

public class KrdsApiOptionsTests
{
    [Fact]
    public void Defaults_AreInitializedCorrectly()
    {
        var options = new KrdsApiOptions();

        Assert.Equal("KrdsApi", KrdsApiOptions.SectionName);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Null(options.GatewayApiKey);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
    }

    [Fact]
    public void Properties_CanBeAssigned()
    {
        var options = new KrdsApiOptions
        {
            BaseUrl = "https://fake-service.test/krds/",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            GatewayApiKey = "test-gateway-key",
            GetHoldingDetailsTimeout = 30_000,
            SearchHoldingsTimeout = 40_000,
            GetUserAccountTimeout = 50_000,
            EnsureUserAccountTimeout = 20_000,
        };

        Assert.Equal("https://fake-service.test/krds/", options.BaseUrl);
        Assert.Equal("test-client-id", options.ClientId);
        Assert.Equal("test-client-secret", options.ClientSecret);
        Assert.Equal("test-gateway-key", options.GatewayApiKey);
        Assert.Equal(30_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(40_000, options.SearchHoldingsTimeout);
        Assert.Equal(50_000, options.GetUserAccountTimeout);
        Assert.Equal(20_000, options.EnsureUserAccountTimeout);
    }

    [Fact]
    public void Validation_Passes_WhenAllRequiredFieldsArePopulated()
    {
        var options = new KrdsApiOptions
        {
            BaseUrl = "http://localhost:3000/krds/",
            ClientId = "client",
            ClientSecret = "secret",
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "client", "secret")]
    [InlineData("http://localhost:3000/krds/", "", "secret")]
    [InlineData("http://localhost:3000/krds/", "client", "")]
    public void Validation_Fails_WhenRequiredFieldsAreEmpty(string baseUrl, string clientId, string clientSecret)
    {
        var options = new KrdsApiOptions
        {
            BaseUrl = baseUrl,
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void AppSettings_KrdsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var options = configuration.GetSection(KrdsApiOptions.SectionName).Get<KrdsApiOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://lis-fake-service/krds/", options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
    }

    [Fact]
    public void AppSettingsDevelopment_KrdsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var options = configuration.GetSection(KrdsApiOptions.SectionName).Get<KrdsApiOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://localhost:3000/krds/", options.BaseUrl);
        Assert.Equal("local-dev-krds-client", options.ClientId);
        Assert.Equal("local-dev-krds-secret", options.ClientSecret);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
    }
}
