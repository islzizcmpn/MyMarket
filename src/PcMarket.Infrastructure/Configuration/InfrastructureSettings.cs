namespace PcMarket.Infrastructure.Configuration;

/// <summary>Redis connection settings (bound from the <c>Redis</c> configuration section).</summary>
public sealed class RedisSettings
{
    public string Configuration { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "pcmarket:";
}

/// <summary>MinIO/S3 object-storage settings (bound from the <c>Minio</c> configuration section).</summary>
public sealed class MinioSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "pcmarket-media";
    public bool UseSsl { get; set; }
}
