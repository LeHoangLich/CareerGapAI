using Amazon.S3;
using Amazon.S3.Model;

namespace CareerGap.Api.Files;

public interface IFileStorage
{
    string Provider { get; }
    Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
}
public sealed class LocalFileStorage(IWebHostEnvironment env, IConfiguration config) : IFileStorage
{
    public string Provider => "Local";
    private readonly string root = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../.local/files"));
    private string Resolve(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) throw new InvalidOperationException("Invalid object key");
        return Path.Combine(root, key);
    }
    public async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        await using var stream = new FileStream(Resolve(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await stream.WriteAsync(bytes, ct);
    }
    public Task<byte[]> ReadAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(Resolve(key), ct);
}
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    public string Provider => "S3";
    private readonly AmazonS3Client client;
    private readonly string bucket;
    public S3FileStorage(IConfiguration config)
    {
        var endpoint = new Uri(config["Storage:Endpoint"] ?? throw new InvalidOperationException("Storage endpoint required"));
        if (endpoint.Scheme != "https") throw new InvalidOperationException("Storage requires HTTPS");
        bucket = config["Storage:Bucket"] ?? throw new InvalidOperationException("Private bucket required");
        client = new AmazonS3Client(config["Storage:AccessKey"], config["Storage:SecretKey"],
            new AmazonS3Config { ServiceURL = endpoint.ToString(), ForcePathStyle = true });
    }
    public async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes, false);
        await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = stream, ContentType = contentType, IfNoneMatch = "*" }, ct);
    }
    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        using var response = await client.GetObjectAsync(bucket, key, ct);
        if (response.ContentLength > DocumentParser.MaxBytes) throw new InvalidDataException("Stored file exceeds limit");
        using var output = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await response.ResponseStream.ReadAsync(chunk, ct)) > 0)
        {
            if (output.Length + count > DocumentParser.MaxBytes) throw new InvalidDataException("Stored file exceeds limit");
            await output.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        return output.ToArray();
    }
    public void Dispose() => client.Dispose();
}
