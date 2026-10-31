using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.Audit.Entities;

public enum MaterialityBenchmark
{
    Revenue = 0,       // 0.5% to 2.0%
    TotalAssets = 1,   // 0.5% to 2.0%
    Equity = 2,        // 1.0% to 5.0%
    PreTaxProfit = 3   // 5.0% to 10.0%
}

public enum EngagementStatus
{
    Planning = 0,
    Fieldwork = 1,
    Review = 2,
    SignedAndIssued = 3
}

public sealed class AuditEngagement : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string Title { get; set; } = string.Empty;
    public MaterialityBenchmark Benchmark { get; set; } = MaterialityBenchmark.Revenue;
    public decimal BenchmarkAmount { get; set; }
    public decimal MaterialityPercentage { get; set; }

    /// <summary>Overall Materiality (Seuil de Signification globale - SSG)</summary>
    public decimal PlanningMateriality { get; set; }

    /// <summary>Performance Materiality / Tolerable Error (Erreur Tolérable - ET, typically 50-75% of SSG)</summary>
    public decimal PerformanceMateriality { get; set; }

    /// <summary>Clearly Trivial Threshold (Seuil de remontée des anomalies, typically 3-5% of SSG)</summary>
    public decimal ClearlyTrivialThreshold { get; set; }

    public EngagementStatus Status { get; set; } = EngagementStatus.Planning;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AuditSampleItem> Samples { get; set; } = [];
}

public enum SamplingMethod
{
    MonetaryUnitSampling = 0,
    StratifiedRandom = 1
}

public sealed class AuditSampleItem : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid AuditEngagementId { get; set; }
    public string PopulationName { get; set; } = string.Empty;
    public string ItemIdentifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BookValue { get; set; }

    public SamplingMethod Method { get; set; }
    public string Stratum { get; set; } = string.Empty; // TopStratum, High, Medium, Low
    public bool IsSelected { get; set; }
    public string? AuditorNotes { get; set; }

    // Navigation
    public AuditEngagement AuditEngagement { get; set; } = null!;
}

public sealed class AuditDeliverable : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string DeliverableType { get; set; } = string.Empty; // Bilan, PvInventaire, AuditReport
    public string Version { get; set; } = "v1.0";
    public string Sha256Hash { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;

    public Guid SignedByUserId { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    public string CryptographicSignature { get; set; } = string.Empty;
}
