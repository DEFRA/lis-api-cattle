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
        // Act
        var options = new KrdsApiOptions();

        // Assert
        Assert.Equal("KrdsApi", KrdsApiOptions.SectionName);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Null(options.GatewayApiKey);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
        Assert.Null(options.CircuitBreakerConsecutiveFailureThreshold);
        Assert.Null(options.CircuitBreakerFailureRatio);
        Assert.Equal(30_000, options.CircuitBreakerSamplingDuration);
        Assert.Equal(30_000, options.CircuitBreakerDurationOfBreak);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
    }

    [Fact]
    public void Properties_CanBeAssigned()
    {
        // Act
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
            CircuitBreakerConsecutiveFailureThreshold = 5,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDuration = 30_000,
            CircuitBreakerDurationOfBreak = 30_000,
            CircuitBreakerMinimumThroughput = 2,
        };

        // Assert
        Assert.Equal("https://fake-service.test/krds/", options.BaseUrl);
        Assert.Equal("test-client-id", options.ClientId);
        Assert.Equal("test-client-secret", options.ClientSecret);
        Assert.Equal("test-gateway-key", options.GatewayApiKey);
        Assert.Equal(30_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(40_000, options.SearchHoldingsTimeout);
        Assert.Equal(50_000, options.GetUserAccountTimeout);
        Assert.Equal(20_000, options.EnsureUserAccountTimeout);
        Assert.Equal(5, options.CircuitBreakerConsecutiveFailureThreshold);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(30_000, options.CircuitBreakerSamplingDuration);
        Assert.Equal(30_000, options.CircuitBreakerDurationOfBreak);
        Assert.Equal(2, options.CircuitBreakerMinimumThroughput);
    }

    [Fact]
    public void Validation_Passes_WhenAllRequiredFieldsArePopulated()
    {
        // Arrange
        var options = new KrdsApiOptions
        {
            BaseUrl = "http://localhost:3000/krds/",
            ClientId = "client",
            ClientSecret = "secret",
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "client", "secret")]
    [InlineData("http://localhost:3000/krds/", "", "secret")]
    [InlineData("http://localhost:3000/krds/", "client", "")]
    public void Validation_Fails_WhenRequiredFieldsAreEmpty(string baseUrl, string clientId, string clientSecret)
    {
        // Arrange
        var options = new KrdsApiOptions
        {
            BaseUrl = baseUrl,
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void AppSettings_KrdsConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        // Act
        var options = configuration.GetSection(KrdsApiOptions.SectionName).Get<KrdsApiOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("http://lis-fake-service/krds/", options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
        Assert.Equal(5, options.CircuitBreakerConsecutiveFailureThreshold);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(30_000, options.CircuitBreakerSamplingDuration);
        Assert.Equal(30_000, options.CircuitBreakerDurationOfBreak);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
    }

    [Fact]
    public void AppSettingsDevelopment_KrdsConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        // Act
        var options = configuration.GetSection(KrdsApiOptions.SectionName).Get<KrdsApiOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("http://localhost:3000/krds/", options.BaseUrl);
        Assert.Equal("local-dev-krds-client", options.ClientId);
        Assert.Equal("local-dev-krds-secret", options.ClientSecret);
        Assert.Equal(60_000, options.GetHoldingDetailsTimeout);
        Assert.Equal(60_000, options.SearchHoldingsTimeout);
        Assert.Equal(60_000, options.GetUserAccountTimeout);
        Assert.Equal(60_000, options.EnsureUserAccountTimeout);
        Assert.Equal(5, options.CircuitBreakerConsecutiveFailureThreshold);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(30_000, options.CircuitBreakerSamplingDuration);
        Assert.Equal(30_000, options.CircuitBreakerDurationOfBreak);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
    }
}
