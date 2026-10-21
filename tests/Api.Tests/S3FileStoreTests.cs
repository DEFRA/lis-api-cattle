// <copyright file="S3FileStoreTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Defra.Lis.Api.Services;
using Moq;

public class S3FileStoreTests
{
    private readonly Mock<IAmazonS3> s3 = new();

    [Fact]
    public async Task GetAsync_KeepsTheStreamOpenUntilTheCallerDisposesIt()
    {
        var data = new MemoryStream(Encoding.UTF8.GetBytes("animal"));
        s3.Setup(c => c.GetObjectAsync("bucket", "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = data });

        var stream = await new S3FileStore(s3.Object).GetAsync("bucket", "key", TestContext.Current.CancellationToken);

        Assert.NotNull(stream);
        Assert.True(data.CanRead);
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("animal", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        Assert.False(data.CanRead);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForMissingObjects()
    {
        s3.Setup(c => c.GetObjectAsync("bucket", "key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Missing") { StatusCode = HttpStatusCode.NotFound });

        Assert.Null(await new S3FileStore(s3.Object).GetAsync("bucket", "key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_PropagatesAccessDenied()
    {
        s3.Setup(c => c.GetObjectAsync("bucket", "key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Denied") { StatusCode = HttpStatusCode.Forbidden });

        await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            new S3FileStore(s3.Object).GetAsync("bucket", "key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertAsync_PreservesCallerOwnershipAndPassesCancellation()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var token = TestContext.Current.CancellationToken;
        s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), token))
            .ReturnsAsync(new PutObjectResponse());

        await new S3FileStore(s3.Object).UpsertAsync("bucket", "key", stream, token);

        s3.Verify(c => c.PutObjectAsync(It.Is<PutObjectRequest>(r => r.BucketName == "bucket" && r.Key == "key" && r.InputStream == stream && !r.AutoCloseStream), token), Times.Once);
        Assert.True(stream.CanRead);
    }
}
