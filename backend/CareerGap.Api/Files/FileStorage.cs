using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace CareerGap.Api.Files;

public interface IFileStorage
{
    string Provider { get; }
    Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
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
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    public string Provider => "S3";
    private readonly AmazonS3Client client;
    private readonly string bucket;
    private readonly ILogger<S3FileStorage>? logger;

    public S3FileStorage(IConfiguration config, ILogger<S3FileStorage>? logger = null)
    {
        this.logger = logger;
        bucket = (config["Storage:Bucket"] ?? throw new InvalidOperationException("Private bucket required")).Trim();
        var rawEndpoint = config["Storage:Endpoint"] ?? throw new InvalidOperationException("Storage endpoint required");
        var (serviceUrl, region) = NormalizeS3Endpoint(rawEndpoint, bucket, config["Storage:Region"]);
        var accessKey = (config["Storage:AccessKey"] ?? string.Empty).Trim();
        var secretKey = (config["Storage:SecretKey"] ?? string.Empty).Trim();
        client = new AmazonS3Client(accessKey, secretKey,
            new AmazonS3Config
            {
                ServiceURL = serviceUrl.ToString(),
                ForcePathStyle = true,
                AuthenticationRegion = region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public static (Uri ServiceUrl, string Region) NormalizeS3Endpoint(string rawEndpoint, string bucket, string? configuredRegion)
    {
        var endpoint = new Uri(rawEndpoint.Trim());
        if (endpoint.Scheme != "https") throw new InvalidOperationException("Storage requires HTTPS");
        var trimmedBucket = bucket.Trim();
        if (!string.IsNullOrEmpty(trimmedBucket) &&
            string.Equals(endpoint.AbsolutePath.Trim('/'), trimmedBucket, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri(endpoint.GetLeftPart(UriPartial.Authority));
        }
        var region = string.IsNullOrWhiteSpace(configuredRegion)
            ? (endpoint.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase) ? "auto" : "us-east-1")
            : configuredRegion.Trim();
        return (endpoint, region);
    }

    public async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes, false);
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                IfNoneMatch = "*",
            }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            logger?.LogError(ex, "S3 PutObject failed (bucket={Bucket}, status={Status}, code={Code}): {Message}",
                bucket, (int)ex.StatusCode, ex.ErrorCode, ex.Message);
            throw;
        }
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        try
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
        catch (AmazonS3Exception ex)
        {
            logger?.LogError(ex, "S3 GetObject failed (bucket={Bucket}, status={Status}, code={Code}): {Message}",
                bucket, (int)ex.StatusCode, ex.ErrorCode, ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try
        {
            await client.DeleteObjectAsync(bucket, key, ct);
        }
        catch (AmazonS3Exception ex)
        {
            logger?.LogError(ex, "S3 DeleteObject failed (bucket={Bucket}, status={Status}, code={Code}): {Message}",
                bucket, (int)ex.StatusCode, ex.ErrorCode, ex.Message);
            throw;
        }
    }

    public void Dispose() => client.Dispose();
}
