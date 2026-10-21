// <copyright file="CadsDataPatchStore.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Services.Upstream;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Manages the temporary CADS patch, with one {cph}/{earTag}.json file per animal.
/// </summary>
public sealed partial class CadsDataPatchStore(
    IS3FileStore s3FileStore,
    IOptions<CadsDataPatchOptions> options,
    ILogger<CadsDataPatchStore> logger) : ICadsDataPatchStore
{
    private const string FileExtension = ".json";

    public async Task<IReadOnlyList<string>> GetCphListAsync(CancellationToken cancellationToken = default)
    {
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return [];
        }

        var keys = await s3FileStore.ListAsync(bucket, cancellationToken: cancellationToken);
        return
        [
            .. keys
                .Where(key => PatchKeyPattern().IsMatch(key))
                .Select(key => key[..key.LastIndexOf('/')])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];
    }

    public async Task<IReadOnlyList<string>> GetCphEarTagsAsync(string cph, CancellationToken cancellationToken = default)
    {
        var prefix = FolderOf(cph);
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return [];
        }

        var keys = await s3FileStore.ListAsync(bucket, prefix, cancellationToken);
        return
        [
            .. keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal) && PatchKeyPattern().IsMatch(key))
                .Select(key => key[prefix.Length..^FileExtension.Length])
        ];
    }

    public async Task<bool> UpsertAnimalAsync(string cph, string earTag, string json, CancellationToken cancellationToken = default)
    {
        var key = AnimalKey(cph, earTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CadsAnimal? animal;
        try
        {
            animal = JsonSerializer.Deserialize<CadsAnimal>(json, UpstreamJson.Options);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The file must contain a valid CADS animal JSON object.", nameof(json), ex);
        }

        if (!string.Equals(animal?.Identifier?.Identifier, earTag, StringComparison.Ordinal))
        {
            throw new ArgumentException("The animal identifier must match the ear tag in the path.", nameof(json));
        }

        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return false;
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await s3FileStore.UpsertAsync(bucket, key, stream, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default)
    {
        var key = AnimalKey(cph, earTag);
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return false;
        }

        await s3FileStore.DeleteAsync(bucket, key, cancellationToken);
        return true;
    }

    public async Task<CadsAnimal?> GetAnimalAsync(string cph, string earTag, CancellationToken cancellationToken = default)
    {
        var key = AnimalKey(cph, earTag);
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return null;
        }

        using var stream = await s3FileStore.GetAsync(bucket, key, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CadsAnimal>(stream, UpstreamJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            LogInvalidPatchFile(earTag, cph);
            return null;
        }
    }

    public async Task<CadsAnimalDetail?> GetAnimalDetailsAsync(string earTag, CancellationToken cancellationToken = default)
    {
        ValidateEarTag(earTag);
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return null;
        }

        var keys = await s3FileStore.ListAsync(bucket, cancellationToken: cancellationToken);

        var key = keys
            .Where(key =>
                PatchKeyPattern().IsMatch(key) &&
                string.Equals(key[(key.LastIndexOf('/') + 1)..^FileExtension.Length], earTag, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).FirstOrDefault();
        if (key is null)
        {
            return null;
        }

        using var stream = await s3FileStore.GetAsync(bucket, key, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CadsAnimalDetail>(stream, UpstreamJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            LogInvalidPatchFile(earTag);
            return null;
        }
    }

    private static string FolderOf(string cph)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cph);
        if (!CphPattern().IsMatch(cph))
        {
            throw new ArgumentException("CPH must have the format 22/001/0001.", nameof(cph));
        }

        return $"{cph}/";
    }

    private static string AnimalKey(string cph, string earTag)
    {
        ValidateEarTag(earTag);
        return $"{FolderOf(cph)}{earTag}{FileExtension}";
    }

    private static void ValidateEarTag(string earTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(earTag);
        if (earTag.IndexOfAny(['/', '\\']) >= 0 || earTag is "." or "..")
        {
            throw new ArgumentException("The ear tag must be a single file name without path separators.", nameof(earTag));
        }
    }

    [GeneratedRegex(@"\A[0-9]{2}/[0-9]{3}/[0-9]{4}\z")]
    private static partial Regex CphPattern();

    [GeneratedRegex(@"\A[0-9]{2}/[0-9]{3}/[0-9]{4}/[^/\\]+\.json\z", RegexOptions.IgnoreCase)]
    private static partial Regex PatchKeyPattern();
}
