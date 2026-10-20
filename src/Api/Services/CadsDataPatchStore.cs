// <copyright file="CadsDataPatchStore.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Services.Upstream;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads the temporary CADS data patch from S3. Each holding has a folder named after its CPH
/// (<c>{cph}/</c>) holding one <c>{earTag}.json</c> file per animal, in the CADS animals-on-holding shape.
/// </summary>
public sealed partial class CadsDataPatchStore(
    IAmazonS3 s3,
    IOptions<CadsDataPatchOptions> options,
    ILogger<CadsDataPatchStore> logger)
    : ICadsDataPatchStore
{
    private const string FileExtension = ".json";

    public async Task<IReadOnlyList<string>> GetEarTagsAsync(string cph, CancellationToken cancellationToken = default)
    {
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return [];
        }

        var prefix = FolderOf(cph);
        var earTags = new List<string>();
        var request = new ListObjectsV2Request { BucketName = bucket, Prefix = prefix, };
        ListObjectsV2Response response;

        do
        {
            response = await s3.ListObjectsV2Async(request, cancellationToken);

            foreach (var item in response.S3Objects ?? [])
            {
                var name = item.Key[prefix.Length..];

                // Only files directly in the CPH folder; nested folders and other file types are ignored.
                if (!name.Contains('/', StringComparison.Ordinal) && name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    earTags.Add(name[..^FileExtension.Length]);
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        return earTags;
    }

    public async Task<CadsAnimal?> GetAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default)
    {
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return null;
        }

        try
        {
            using var response = await s3.GetObjectAsync(bucket, $"{FolderOf(cph)}{earTag}{FileExtension}", cancellationToken);
            return await JsonSerializer.DeserializeAsync<CadsAnimal>(response.ResponseStream, UpstreamJson.Options, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (JsonException)
        {
            LogInvalidPatchFile(earTag, cph);
            return null;
        }
    }

    public async Task<CadsAnimalDetail?> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default)
    {
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return null;
        }

        try
        {
            var animalObject = await FindAnimalObjectAsync(bucket, earTag, cancellationToken);
            if (animalObject is null)
            {
                return null;
            }

            using var response = await s3.GetObjectAsync(bucket, animalObject.Key, cancellationToken);
            return await JsonSerializer.DeserializeAsync<CadsAnimalDetail>(response.ResponseStream, UpstreamJson.Options, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (JsonException)
        {
            LogInvalidPatchFile(earTag);
            return null;
        }
    }

    private static bool IsAnimalFile(string key, string fileName)
    {
        // Keys are {cph}/{earTag}.json, and a CPH holds slashes itself (22/001/0001). The file name after the last
        // slash must match exactly, so UK123 does not match XUK123.json.
        var slash = key.LastIndexOf('/');
        return slash > 0 && key.AsSpan(slash + 1).Equals(fileName, StringComparison.Ordinal);
    }

    private static string FolderOf(string cph) => $"{cph.Trim().Trim('/')}/";

    /// <summary>
    /// Finds the file for an ear tag in whichever CPH folder holds it. The bucket is listed page by page, as the
    /// layout does not allow a prefix on the ear tag. When an animal has files under more than one holding (it
    /// has moved), the most recently written file wins.
    /// </summary>
    private async Task<S3Object?> FindAnimalObjectAsync(string bucket, string earTag, CancellationToken cancellationToken)
    {
        var fileName = $"{earTag.Trim()}{FileExtension}";
        var request = new ListObjectsV2Request { BucketName = bucket, };
        var matches = new List<S3Object>();
        ListObjectsV2Response response;

        do
        {
            response = await s3.ListObjectsV2Async(request, cancellationToken);
            matches.AddRange((response.S3Objects ?? []).Where(item => IsAnimalFile(item.Key, fileName)));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        if (matches.Count > 1)
        {
            LogDuplicatePatchFiles(earTag, matches.Count);
        }

        return matches
            .OrderByDescending(item => item.LastModified ?? DateTime.MinValue)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
