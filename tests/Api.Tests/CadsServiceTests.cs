// <copyright file="CadsServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Text.Json;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

public class CadsServiceTests
{
    private readonly Mock<ILogger<CadsService>> mockLogger;

    public CadsServiceTests()
    {
        mockLogger = new Mock<ILogger<CadsService>>();
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsCattle_WhenResponseIsSuccess()
    {
        // Arrange
        var cph = "12/345/6789";
        var cattle = new List<CattleResponse>
        {
            new() { EarTag = "UK123456700001", Breed = "Hereford", Sex = "female", Status = "active" },
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri != null &&
                    req.RequestUri.ToString().Contains($"cattle?cph={Uri.EscapeDataString(cph)}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(cattle),
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://cads-api/"), };

        var service = new CadsService(httpClient, mockLogger.Object);

        // Act
        var result = await service.GetCattleByCphAsync(cph);

        // Assert
        Assert.NotNull(result);
        var list = result.ToList();
        Assert.Single(list);
        Assert.Equal("UK123456700001", list[0].EarTag);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsEmpty_WhenResponseIsNonSuccess()
    {
        // Arrange
        var cph = "12/345/6789";
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://cads-api/"),
        };

        var service = new CadsService(httpClient, mockLogger.Object);

        // Act
        var result = await service.GetCattleByCphAsync(cph);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCattleByCphAsync_ReturnsEmpty_WhenHttpExceptionThrown()
    {
        // Arrange
        var cph = "12/345/6789";
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://cads-api/"),
        };

        var service = new CadsService(httpClient, mockLogger.Object);

        // Act
        var result = await service.GetCattleByCphAsync(cph);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
