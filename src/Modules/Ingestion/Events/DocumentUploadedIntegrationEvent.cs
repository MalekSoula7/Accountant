namespace ComptaShield.Ingestion.Events;

/// <summary>
/// Integration event published via MassTransit when a document is uploaded.
/// Triggers downstream OCR/TEIF processing workers.
/// </summary>
public sealed record DocumentUploadedIntegrationEvent
{
    public Guid DocumentId { get; init; }
    public Guid CabinetId { get; init; }
    public Guid DossierId { get; init; }
    public Guid ExerciceId { get; init; }
    public string ObjectStorageKey { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public Guid UploadedByUserId { get; init; }
    public DateTime UploadedAt { get; init; }
}
