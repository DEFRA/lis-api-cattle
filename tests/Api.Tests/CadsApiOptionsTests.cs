// <copyright file="CadsApiOptionsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.ComponentModel.DataAnnotations;
using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Configuration;
using Xunit;

public class CadsApiOptionsTests
{
    [Fact]
    public void Defaults_AreInitializedCorrectly()
    {
        var options = new CadsApiOptions();

        Assert.Equal("CadsApi", CadsApiOptions.SectionName);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Null(options.GatewayApiKey);
        Assert.Equal(100, options.PageSize);
        Assert.Equal(60_000, options.GetAnimalsOnHoldingTimeout);
        Assert.Equal(60_000, options.GetAnimalDetailsTimeout);
    }

    [Fact]
    public void Properties_CanBeAssigned()
    {
        var options = new CadsApiOptions
        {
            BaseUrl = "https://fake-service.test/cads/",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            GatewayApiKey = "test-gateway-key",
            PageSize = 250,
            GetAnimalsOnHoldingTimeout = 30_000,
            GetAnimalDetailsTimeout = 45_000,
        };

        Assert.Equal("https://fake-service.test/cads/", options.BaseUrl);
        Assert.Equal("test-client-id", options.ClientId);
        Assert.Equal("test-client-secret", options.ClientSecret);
        Assert.Equal("test-gateway-key", options.GatewayApiKey);
        Assert.Equal(250, options.PageSize);
        Assert.Equal(30_000, options.GetAnimalsOnHoldingTimeout);
        Assert.Equal(45_000, options.GetAnimalDetailsTimeout);
    }

    [Fact]
    public void Validation_Passes_WhenAllRequiredFieldsArePopulated()
    {
        var options = new CadsApiOptions
        {
            BaseUrl = "http://localhost:3000/cads/",
            ClientId = "client",
            ClientSecret = "secret",
            PageSize = 500,
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "client", "secret")]
    [InlineData("http://localhost:3000/cads/", "", "secret")]
    [InlineData("http://localhost:3000/cads/", "client", "")]
    public void Validation_Fails_WhenRequiredFieldsAreEmpty(string baseUrl, string clientId, string clientSecret)
    {
        var options = new CadsApiOptions
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Validation_Fails_WhenPageSizeIsOutOfRange(int pageSize)
    {
        var options = new CadsApiOptions
        {
            BaseUrl = "http://localhost:3000/cads/",
            ClientId = "client",
            ClientSecret = "secret",
            PageSize = pageSize,
        };

        var context = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CadsApiOptions.PageSize)));
    }

    [Fact]
    public void AppSettings_CadsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var options = configuration.GetSection(CadsApiOptions.SectionName).Get<CadsApiOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://lis-fake-service/cads/", options.BaseUrl);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.ClientSecret);
        Assert.Equal(500, options.PageSize);
        Assert.Equal(60_000, options.GetAnimalsOnHoldingTimeout);
        Assert.Equal(60_000, options.GetAnimalDetailsTimeout);
    }

    [Fact]
    public void AppSettingsDevelopment_CadsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var options = configuration.GetSection(CadsApiOptions.SectionName).Get<CadsApiOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://localhost:3000/cads/", options.BaseUrl);
        Assert.Equal("local-dev-cads-client", options.ClientId);
        Assert.Equal("local-dev-cads-secret", options.ClientSecret);
        Assert.Equal(500, options.PageSize);
        Assert.Equal(60_000, options.GetAnimalsOnHoldingTimeout);
        Assert.Equal(60_000, options.GetAnimalDetailsTimeout);
    }
}
