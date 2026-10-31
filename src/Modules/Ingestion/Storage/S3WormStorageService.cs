using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace ComptaShield.Ingestion.Storage;

/// <summary>
/// MinIO/S3-backed WORM storage service.
/// Inbound path: /{cabinetId}/{dossierId}/{exerciceId}/inbound/{sha256}.{ext}
/// Deliverable path: /{cabinetId}/{dossierId}/{exerciceId}/deliverables/{docType}_{version}_{sha256}.pdf
/// </summary>
public sealed class S3WormStorageService : IWormStorageService
{
    private readonly IMinioClient _minio;
    private readonly ILogger<S3WormStorageService> _logger;
    private const string BucketName = "comptashield-vault";

    public S3WormStorageService(IMinioClient minio, ILogger<S3WormStorageService> logger)
    {
        _minio = minio;
        _logger = logger;
    }

    public async Task<string?> StoreInboundFileAsync(
        Guid cabinetId, Guid dossierId, Guid exerciceId,
        Stream fileStream, string sha256Hash, string fileExtension,
        CancellationToken ct = default)
    {
        var objectKey = BuildInboundKey(cabinetId, dossierId, exerciceId, sha256Hash, fileExtension);

        // Check for SHA-256 duplicate
        try
        {
            await _minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(BucketName).WithObject(objectKey), ct);

            _logger.LogInformation("Duplicate detected: {ObjectKey} already exists", objectKey);
            return null; // Duplicate
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            // Object doesn't exist — proceed with upload
        }

        await _minio.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(GetContentType(fileExtension)),
            ct);

        _logger.LogInformation("Stored inbound file: {ObjectKey}", objectKey);
        return objectKey;
    }

    public async Task<string> StoreDeliverableAsync(
        Guid cabinetId, Guid dossierId, Guid exerciceId,
        Stream fileStream, string docType, string version, string sha256Hash,
        CancellationToken ct = default)
    {
        var objectKey = BuildDeliverableKey(cabinetId, dossierId, exerciceId, docType, version, sha256Hash);

        await _minio.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType("application/pdf"),
            ct);

        _logger.LogInformation("Stored deliverable: {ObjectKey}", objectKey);
        return objectKey;
    }

    public async Task<Stream> GetFileAsync(string objectKey, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _minio.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(ms)),
            ct);

        ms.Position = 0;
        return ms;
    }

    public async Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiry, CancellationToken ct = default)
    {
        var url = await _minio.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds));

        return url;
    }

    private static string BuildInboundKey(Guid cabinetId, Guid dossierId, Guid exerciceId, string sha256, string ext)
        => $"{cabinetId}/{dossierId}/{exerciceId}/inbound/{sha256}.{ext.TrimStart('.')}";

    private static string BuildDeliverableKey(Guid cabinetId, Guid dossierId, Guid exerciceId, string docType, string version, string sha256)
        => $"{cabinetId}/{dossierId}/{exerciceId}/deliverables/{docType}_{version}_{sha256}.pdf";

    private static string GetContentType(string ext) => ext.ToLowerInvariant().TrimStart('.') switch
    {
        "pdf" => "application/pdf",
        "xml" => "application/xml",
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "tiff" or "tif" => "image/tiff",
        _ => "application/octet-stream"
    };
}
