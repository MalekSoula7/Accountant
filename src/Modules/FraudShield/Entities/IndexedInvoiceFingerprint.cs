using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.FraudShield.Entities;

/// <summary>
/// Fingerprint record of processed invoices used for cross-exercice duplicate detection.
/// </summary>
public sealed class IndexedInvoiceFingerprint : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid DocumentId { get; set; }
    public string SupplierMatriculeFiscal { get; set; } = string.Empty;
    public string NormalizedInvoiceNumber { get; set; } = string.Empty;
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalTTC { get; set; }

    /// <summary>64-bit perceptual hash represented as unsigned 64-bit integer.</summary>
    public ulong PerceptualHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
