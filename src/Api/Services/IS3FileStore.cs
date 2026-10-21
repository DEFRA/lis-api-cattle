// <copyright file="IS3FileStore.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

/// <summary>
/// Browses and manages files in S3 buckets.
/// </summary>
public interface IS3FileStore
{
    /// <summary>
    /// Lists all file keys matching the optional prefix, including results from every S3 page.
    /// A null prefix lists the entire bucket; keys include their full path within the bucket.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<IReadOnlyList<string>> ListAsync(string bucket, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the contents of the file at the given key as a stream, or null when the key does not exist.
    /// The caller must dispose the returned stream.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<Stream?> GetAsync(string bucket, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the contents of the given stream to the file at the given key, replacing any existing object.
    /// The caller retains ownership of the input stream.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task UpsertAsync(string bucket, string key, Stream data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the file at the given key. A missing key is treated as success.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default);
}
