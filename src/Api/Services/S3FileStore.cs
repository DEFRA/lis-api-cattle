// <copyright file="S3FileStore.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

public sealed class S3FileStore(IAmazonS3 s3)
    : IS3FileStore
{
    public async Task<IReadOnlyList<string>> ListAsync(string bucket, string? prefix = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(s3);

        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            keys.AddRange((response.S3Objects ?? []).Select(item => item.Key));
            continuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                : null;
        }
        while (continuationToken is not null);

        return keys;
    }

    public async Task<Stream?> GetAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(s3);

        try
        {
            var response = await s3.GetObjectAsync(bucket, key, cancellationToken);
            return new ObjectReadStream(response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(string bucket, string key, Stream data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(s3);

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = data,
            AutoCloseStream = false,
        };
        await s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(s3);

        var request = new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = key,
        };
        await s3.DeleteObjectAsync(request, cancellationToken);
    }

    // The caller owns the returned stream; disposing it also releases the S3 response.
    private sealed class ObjectReadStream(GetObjectResponse response) : Stream
    {
        public override bool CanRead => Inner.CanRead;

        public override bool CanSeek => Inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => Inner.Length;

        public override long Position { get => Inner.Position; set => Inner.Position = value; }

        public override void Flush() => Inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                response.Dispose();
            }

            base.Dispose(disposing);
        }

        private Stream Inner => response.ResponseStream;
    }
}
