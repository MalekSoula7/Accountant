using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.LedgerAndBilan.Entities;

/// <summary>
/// A line within a double-entry journal transaction.
/// Scoped to CabinetId, DossierId, ExerciceId.
/// </summary>
public sealed class JournalEntryLine : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public Guid JournalEntryId { get; set; }
    public int LineNumber { get; set; }

    public string AccountNumber { get; set; } = string.Empty;
    public string AccountLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public string? LetteringCode { get; set; }

    // Navigation
    public JournalEntry JournalEntry { get; set; } = null!;
}
