using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.FraudShield.Entities;

public enum FraudAlertType
{
    TamperedPdfMetadata = 0,
    FontAnomaly = 1,
    SemanticDuplicate = 2,
    PerceptualHashDuplicate = 3,
    ThreeWayMismatch = 4,
    BenfordLawAnomaly = 5,
    ThresholdSplitting = 6
}

public enum FraudSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum FraudAlertStatus
{
    Active = 0,
    Quarantined = 1,
    ClarificationRequested = 2,
    Dismissed = 3,
    ConfirmedFraud = 4,
    Rectified = 5
}

/// <summary>
/// Operational entity recording forensic anomalies detected on inbound documents or ledger entries.
/// Scoped to CabinetId, DossierId, ExerciceId.
/// </summary>
public sealed class FraudAlert : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid? DocumentId { get; set; }
    public Guid? JournalEntryId { get; set; }

    public FraudAlertType AlertType { get; set; }
    public FraudSeverity Severity { get; set; }
    public FraudAlertStatus Status { get; set; } = FraudAlertStatus.Active;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ForensicEvidenceJson { get; set; } = string.Empty;

    public decimal RiskScore { get; set; } // 0.00 to 1.00

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
}
