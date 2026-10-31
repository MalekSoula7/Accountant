namespace ComptaShield.Ingestion.Storage;

/// <summary>
/// Abstraction for WORM (Write Once Read Many) object storage operations.
/// Files are stored at /{cabinetId}/{dossierId}/{exerciceId}/inbound/{sha256}.{ext}
/// Deliverables at /{cabinetId}/{dossierId}/{exerciceId}/deliverables/{docType}_{version}_{sha256}.pdf
/// </summary>
public interface IWormStorageService
{
    /// <summary>
    /// Stores a file in the inbound vault. Returns the object key.
    /// If a file with the same SHA-256 already exists, returns null (duplicate).
    /// </summary>
    Task<string?> StoreInboundFileAsync(
        Guid cabinetId, Guid dossierId, Guid exerciceId,
        Stream fileStream, string sha256Hash, string fileExtension,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a signed deliverable (Bilan PDF, Audit Report, etc.) in the deliverables vault.
    /// </summary>
    Task<string> StoreDeliverableAsync(
        Guid cabinetId, Guid dossierId, Guid exerciceId,
        Stream fileStream, string docType, string version, string sha256Hash,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a file stream by its full object key.
    /// </summary>
    Task<Stream> GetFileAsync(string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Generates a pre-signed URL for temporary read access.
    /// </summary>
    Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiry, CancellationToken ct = default);
}
