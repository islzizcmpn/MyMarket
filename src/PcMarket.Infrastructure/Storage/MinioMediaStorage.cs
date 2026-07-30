using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using PcMarket.Application.Abstractions.Storage;
using PcMarket.Infrastructure.Configuration;

namespace PcMarket.Infrastructure.Storage;

/// <summary>MinIO/S3-backed <see cref="IMediaStorage"/>. Ensures the target bucket exists before writing.</summary>
public sealed class MinioMediaStorage(IMinioClient client, IOptions<MinioSettings> options) : IMediaStorage
{
    private readonly MinioSettings _settings = options.Value;

    public async Task<string> UploadAsync(
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        await client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectName)
                .WithStreamData(content)
                .WithObjectSize(content.CanSeek ? content.Length : -1)
                .WithContentType(contentType),
            cancellationToken);

        return objectName;
    }

    public async Task<Stream> GetAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var memory = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memory)),
            cancellationToken);

        memory.Position = 0;
        return memory;
    }

    public Task<string> GetPresignedUrlAsync(string objectName, TimeSpan expiry, CancellationToken cancellationToken = default) =>
        client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectName)
                .WithExpiry((int)expiry.TotalSeconds));

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default) =>
        client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectName),
            cancellationToken);

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_settings.Bucket), cancellationToken);

        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_settings.Bucket), cancellationToken);
        }
    }
}
