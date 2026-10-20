// <copyright file="CadsDataPatchStoreTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class CadsDataPatchStoreTests
{
    private const string Bucket = "cads-data-patch";
    private const string Cph = "22/001/0001";

    private readonly Mock<IAmazonS3> s3 = new();

    [Fact]
    public async Task GetEarTagsAsync_ListsJsonFilesInTheCphFolderAcrossPages()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.ContinuationToken == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing("next", $"{Cph}/UK200000000001.json", $"{Cph}/readme.txt", $"{Cph}/archive/UK200000000009.json"));
        s3.Setup(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.ContinuationToken == "next"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/UK200000000002.JSON"));
        var store = CreateStore(Bucket);

        var earTags = await store.GetEarTagsAsync(Cph, TestContext.Current.CancellationToken);

        Assert.Equal(["UK200000000001", "UK200000000002"], earTags);
        s3.Verify(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.BucketName == Bucket && r.Prefix == $"{Cph}/"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetEarTagsAsync_ReturnsNothingWithoutCallingS3_WhenNoBucketIsConfigured()
    {
        var store = CreateStore(bucket: null);

        var earTags = await store.GetEarTagsAsync(Cph, TestContext.Current.CancellationToken);

        Assert.Empty(earTags);
        s3.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAnimalAsync_ReadsTheAnimalFromItsEarTagFile()
    {
        s3.Setup(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object("""
                {
                  "identifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "UK200000000001" },
                  "birthDate": "2023-02-01",
                  "sex": "Female",
                  "breedCode": { "schema": "cts.breed", "breedName": "Aberdeen Angus", "identifier": "AA" },
                  "status": "Alive"
                }
                """));
        var store = CreateStore(Bucket);

        var animal = await store.GetAnimalAsync(Cph, "UK200000000001", TestContext.Current.CancellationToken);

        Assert.NotNull(animal);
        Assert.Equal("UK200000000001", animal.Identifier?.Identifier);
        Assert.Equal(new DateOnly(2023, 2, 1), animal.BirthDate);
        Assert.Equal("AA", animal.BreedCode?.Identifier);
        Assert.True(animal.IsAlive);
    }

    [Fact]
    public async Task GetAnimalAsync_ReturnsNull_WhenTheFileIsNotValidJson()
    {
        s3.Setup(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object("not json"));
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalAsync(Cph, "UK200000000001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalAsync_ReturnsNull_WhenTheFileHasGone()
    {
        s3.Setup(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalAsync(Cph, "UK200000000001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetEarTagsAsync_ReturnsNothing_WhenTheCphFolderIsEmpty()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = null, IsTruncated = false });
        var store = CreateStore(Bucket);

        Assert.Empty(await store.GetEarTagsAsync(Cph, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalAsync_ReturnsNullWithoutCallingS3_WhenNoBucketIsConfigured()
    {
        var store = CreateStore(bucket: " ");

        Assert.Null(await store.GetAnimalAsync(Cph, "UK200000000001", TestContext.Current.CancellationToken));
        s3.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReadsTheAnimalFromWhicheverFolderHoldsItsEarTagFile()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.BucketName == Bucket), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/UK200000000002.json", "33/002/0002/UK200000000001.json"));
        s3.Setup(c => c.GetObjectAsync(Bucket, "33/002/0002/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object("""
                {
                  "resourceType": "Animal",
                  "identifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "UK200000000001" },
                  "species": "Cattle",
                  "sex": "Male",
                  "birthDate": "2023-02-01",
                  "breedCode": { "schema": "cts.breed", "breedName": "Aberdeen Angus", "identifier": "AA" },
                  "state": "Alive"
                }
                """));
        var store = CreateStore(Bucket);

        var animal = await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken);

        Assert.NotNull(animal);
        Assert.Equal("UK200000000001", animal.Identifier?.Identifier);
        Assert.Equal("Male", animal.Sex);
        Assert.Equal(new DateOnly(2023, 2, 1), animal.BirthDate);
        Assert.Equal("AA", animal.BreedCode?.Identifier);
        Assert.Equal("Alive", animal.State);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReturnsNull_WhenNoFileHoldsTheEarTag()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/UK200000000002.json"));
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
        s3.Verify(c => c.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReturnsNull_WhenTheFileIsNotValidJson()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/UK200000000001.json"));
        s3.Setup(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object("not json"));
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReturnsNull_WhenTheFileHasGone()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/UK200000000001.json"));
        s3.Setup(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReturnsNullWithoutCallingS3_WhenNoBucketIsConfigured()
    {
        var store = CreateStore(bucket: null);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
        s3.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_FindsTheEarTagFileOnALaterPage()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.ContinuationToken == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing("page-2", $"{Cph}/UK200000000002.json"));
        s3.Setup(c => c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.ContinuationToken == "page-2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, "33/002/0002/UK200000000001.json"));
        s3.Setup(c => c.GetObjectAsync(Bucket, "33/002/0002/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object(AnimalDetail("UK200000000001", "Male")));
        var store = CreateStore(Bucket);

        var animal = await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken);

        Assert.Equal("UK200000000001", animal?.Identifier?.Identifier);
        s3.Verify(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_DoesNotMatchAnEarTagThatOnlyEndsWithTheRequestedOne()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing(null, $"{Cph}/XUK200000000001.json", $"{Cph}/UK200000000001.json.bak", "UK200000000001.json"));
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
        s3.Verify(c => c.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReadsTheMostRecentFile_WhenTheEarTagIsUnderMoreThanOneHolding()
    {
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects =
                [
                    new S3Object { Key = $"{Cph}/UK200000000001.json", LastModified = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new S3Object { Key = "33/002/0002/UK200000000001.json", LastModified = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                ],
                IsTruncated = false,
            });
        s3.Setup(c => c.GetObjectAsync(Bucket, "33/002/0002/UK200000000001.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Object(AnimalDetail("UK200000000001", "Female")));
        var store = CreateStore(Bucket);

        var animal = await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken);

        Assert.Equal("Female", animal?.Sex);
        s3.Verify(c => c.GetObjectAsync(Bucket, $"{Cph}/UK200000000001.json", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAnimalDetailsAsync_ReturnsNull_WhenTheBucketIsEmpty()
    {
        // S3 leaves S3Objects null, not empty, when nothing matches.
        s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = null, IsTruncated = false });
        var store = CreateStore(Bucket);

        Assert.Null(await store.GetAnimalDetailsAsync("UK200000000001", TestContext.Current.CancellationToken));
    }

    private static string AnimalDetail(string earTag, string sex) =>
        $$"""{ "identifier": { "schema": "uk.gov.defra.ear-tag.conventional", "identifier": "{{earTag}}" }, "sex": "{{sex}}" }""";

    private static ListObjectsV2Response Listing(string? nextToken, params string[] keys) => new()
    {
        S3Objects = keys.Select(key => new S3Object { Key = key }).ToList(),
        IsTruncated = nextToken is not null,
        NextContinuationToken = nextToken,
    };

    private static GetObjectResponse Object(string body) => new()
    {
        ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(body)),
    };

    private CadsDataPatchStore CreateStore(string? bucket) => new(
        s3.Object,
        Microsoft.Extensions.Options.Options.Create(new CadsDataPatchOptions { BucketName = bucket }),
        NullLogger<CadsDataPatchStore>.Instance);
}
