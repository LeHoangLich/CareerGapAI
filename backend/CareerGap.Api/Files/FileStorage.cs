using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace CareerGap.Api.Files;

public sealed class StorageConfigurationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed record S3ResolvedSettings(
    string Bucket,
    Uri? ServiceUrl,
    string Region,
    string AccessKey,
    string SecretKey,
    string? ConfigurationError);

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
    private readonly AmazonS3Client? client;
    private readonly string bucket;
    private readonly string? configurationError;
    private readonly ILogger<S3FileStorage>? logger;

    public S3FileStorage(IConfiguration config, ILogger<S3FileStorage>? logger = null)
    {
        this.logger = logger;
        var settings = ResolveS3Settings(config);
        bucket = settings.Bucket;
        configurationError = settings.ConfigurationError;
        if (configurationError is not null || settings.ServiceUrl is null)
        {
            logger?.LogError("S3 storage configuration invalid: {Error}", configurationError);
            return;
        }
        client = new AmazonS3Client(settings.AccessKey, settings.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl.ToString(),
                ForcePathStyle = true,
                AuthenticationRegion = settings.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                Timeout = TimeSpan.FromSeconds(30),
                HttpClientFactory = new NoExpectContinueHttpClientFactory(),
            });
    }

    public static string CleanConfigValue(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1].Trim();
        }
        return trimmed;
    }

    public static bool LooksLikePlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.StartsWith('<') && value.EndsWith('>')) return true;
        if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')) return true;
        if (value.StartsWith("replace-with-", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("changeme", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public static S3ResolvedSettings ResolveS3Settings(IConfiguration config)
    {
        var bucket = FirstNonEmpty(config["Storage:Bucket"], config["R2_BUCKET"]);
        var rawEndpoint = FirstNonEmpty(config["Storage:Endpoint"], config["R2_ENDPOINT"]);
        var rawRegion = FirstNonEmpty(config["Storage:Region"], config["R2_REGION"]);
        var accessKey = FirstNonEmpty(config["Storage:AccessKey"], config["Storage:AccessKeyId"], config["R2_ACCESS_KEY_ID"]);
        var secretKey = FirstNonEmpty(config["Storage:SecretKey"], config["Storage:SecretAccessKey"], config["R2_SECRET_ACCESS_KEY"]);

        if (LooksLikePlaceholder(bucket))
            return new(bucket, null, "auto", accessKey, secretKey, "Storage__Bucket is missing or still a placeholder.");
        if (LooksLikePlaceholder(rawEndpoint))
            return new(bucket, null, "auto", accessKey, secretKey, "Storage__Endpoint is missing or still a placeholder.");

        Uri serviceUrl;
        string region;
        try
        {
            (serviceUrl, region) = NormalizeS3Endpoint(rawEndpoint, bucket, rawRegion);
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
        {
            return new(bucket, null, "auto", accessKey, secretKey, $"Storage__Endpoint is invalid: {ex.Message}");
        }

        if (LooksLikePlaceholder(accessKey) || LooksLikePlaceholder(secretKey))
        {
            return new(bucket, serviceUrl, region, accessKey, secretKey,
                "Storage__AccessKey or Storage__SecretKey is missing or still contains a placeholder value (e.g. <R2_ACCESS_KEY_ID>).");
        }

        if (serviceUrl.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase))
        {
            if (accessKey.Length != 32)
            {
                return new(bucket, serviceUrl, region, accessKey, secretKey,
                    $"Cloudflare R2 Storage__AccessKey has length {accessKey.Length}, expected 32 characters (use R2 S3 Access Key ID, not a placeholder or API token).");
            }
            if (secretKey.Length != 64)
            {
                return new(bucket, serviceUrl, region, accessKey, secretKey,
                    $"Cloudflare R2 Storage__SecretKey has length {secretKey.Length}, expected 64 characters (use R2 S3 Secret Access Key).");
            }
        }

        return new(bucket, serviceUrl, region, accessKey, secretKey, null);
    }

    public static (Uri ServiceUrl, string Region) NormalizeS3Endpoint(string rawEndpoint, string bucket, string? configuredRegion)
    {
        var cleanedEndpoint = CleanConfigValue(rawEndpoint);
        var endpoint = new Uri(cleanedEndpoint);
        if (endpoint.Scheme != "https") throw new InvalidOperationException("Storage requires HTTPS");
        if (endpoint.Host.EndsWith(".r2.dev", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the Cloudflare R2 S3 API endpoint (*.r2.cloudflarestorage.com), not a public r2.dev URL");
        var trimmedBucket = CleanConfigValue(bucket);
        if (!string.IsNullOrEmpty(trimmedBucket) &&
            string.Equals(endpoint.AbsolutePath.Trim('/'), trimmedBucket, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri(endpoint.GetLeftPart(UriPartial.Authority));
        }
        var cleanedRegion = CleanConfigValue(configuredRegion);
        var region = string.IsNullOrWhiteSpace(cleanedRegion)
            ? (endpoint.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase) ? "auto" : "us-east-1")
            : cleanedRegion;
        return (endpoint, region);
    }

    public async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        var activeClient = EnsureConfigured("PutObject");
        using var stream = new MemoryStream(bytes, false);
        try
        {
            await activeClient.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            logger?.LogError(ex, "S3 PutObject failed (bucket={Bucket}, status={Status}, code={Code}): {Message}",
                bucket, (int)ex.StatusCode, ex.ErrorCode, ex.Message);
            if (IsCredentialConfigurationError(ex))
                throw new StorageConfigurationException("storage_not_configured", $"S3 credential rejected ({ex.ErrorCode}): {ex.Message}");
            throw;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            logger?.LogError(ex, "S3 PutObject transport failed (bucket={Bucket}, type={Type}): {Message}",
                bucket, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        var activeClient = EnsureConfigured("GetObject");
        try
        {
            using var response = await activeClient.GetObjectAsync(bucket, key, ct);
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
            if (IsCredentialConfigurationError(ex))
                throw new StorageConfigurationException("storage_not_configured", $"S3 credential rejected ({ex.ErrorCode}): {ex.Message}");
            throw;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            logger?.LogError(ex, "S3 GetObject transport failed (bucket={Bucket}, type={Type}): {Message}",
                bucket, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var activeClient = EnsureConfigured("DeleteObject");
        try
        {
            await activeClient.DeleteObjectAsync(bucket, key, ct);
        }
        catch (AmazonS3Exception ex)
        {
            logger?.LogError(ex, "S3 DeleteObject failed (bucket={Bucket}, status={Status}, code={Code}): {Message}",
                bucket, (int)ex.StatusCode, ex.ErrorCode, ex.Message);
            if (IsCredentialConfigurationError(ex))
                throw new StorageConfigurationException("storage_not_configured", $"S3 credential rejected ({ex.ErrorCode}): {ex.Message}");
            throw;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            logger?.LogError(ex, "S3 DeleteObject transport failed (bucket={Bucket}, type={Type}): {Message}",
                bucket, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private AmazonS3Client EnsureConfigured(string operation)
    {
        if (client is not null && configurationError is null) return client;
        var message = configurationError ?? "S3 storage client is not configured.";
        logger?.LogError("S3 {Operation} aborted: {Error}", operation, message);
        throw new StorageConfigurationException("storage_not_configured", message);
    }

    private static bool IsCredentialConfigurationError(AmazonS3Exception ex) =>
        ex.ErrorCode is "InvalidAccessKeyId" or "SignatureDoesNotMatch" or "AccessDenied"
        || ex.Message.Contains("Credential access key", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var cleaned = CleanConfigValue(candidate);
            if (!string.IsNullOrEmpty(cleaned)) return cleaned;
        }
        return string.Empty;
    }

    private sealed class NoExpectContinueHttpClientFactory : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                Expect100ContinueTimeout = TimeSpan.Zero,
            };
            var httpClient = new HttpClient(handler)
            {
                Timeout = clientConfig.Timeout ?? TimeSpan.FromSeconds(30),
            };
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            return httpClient;
        }
    }

    public void Dispose() => client?.Dispose();
}
