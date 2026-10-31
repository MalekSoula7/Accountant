using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.Ingestion.Entities;

/// <summary>
/// Represents an uploaded document tracked in the ingestion pipeline.
/// </summary>
public sealed class IngestedDocument : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public string ObjectStorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public DocumentSource Source { get; set; } = DocumentSource.ManualUpload;

    /// <summary>JSONB field storing extracted OCR/TEIF data.</summary>
    public string? ExtractedDataJson { get; set; }

    /// <summary>Overall OCR confidence score (0.00 - 1.00). Null for TEIF XML.</summary>
    public decimal? OcrConfidence { get; set; }

    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

public enum DocumentStatus
{
    Uploaded = 0,
    Processing = 1,
    ReadyForPosting = 2,
    RequiresHumanReview = 3,
    Posted = 4,
    Quarantined = 5,
    Rejected = 6
}

public enum DocumentSource
{
    ManualUpload = 0,
    TeifXml = 1,
    OcrScanned = 2
}
