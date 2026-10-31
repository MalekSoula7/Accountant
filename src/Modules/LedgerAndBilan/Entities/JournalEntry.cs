using ComptaShield.SharedKernel.MultiTenancy;

namespace ComptaShield.LedgerAndBilan.Entities;

public enum JournalEntryStatus
{
    Draft = 0,
    Posted = 1,
    Validated = 2
}

/// <summary>
/// Double-entry journal transaction (Écriture Comptable).
/// Invariant: Sum(Lines.Debit) == Sum(Lines.Credit).
/// Once Posted/Validated, entries are append-only.
/// </summary>
public sealed class JournalEntry : IDossierScopedEntity
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public Guid DossierId { get; set; }
    public Guid ExerciceId { get; set; }

    public string EntryNumber { get; set; } = string.Empty;
    public string JournalCode { get; set; } = string.Empty; // HA, VT, BQ, CA, OD
    public DateTime EntryDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Drill-down link to the original ingested document / invoice.</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>Pointer to the original entry if this is a reversing entry (Contrepassation).</summary>
    public Guid? ReversesEntryId { get; set; }

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public DateTime? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JournalEntryLine> Lines { get; set; } = [];

    /// <summary>
    /// Recalculates totals and validates the double-entry invariant.
    /// </summary>
    public void ValidateDoubleEntryBalance()
    {
        if (Lines.Count == 0)
            throw new InvalidOperationException("Journal entry must contain at least one line.");

        decimal sumDebit = 0m;
        decimal sumCredit = 0m;

        foreach (var line in Lines)
        {
            if (line.Debit < 0m || line.Credit < 0m)
                throw new InvalidOperationException("Negative amounts are forbidden in double-entry ledger lines.");

            if (line.Debit > 0m && line.Credit > 0m)
                throw new InvalidOperationException("A journal entry line cannot have both Debit and Credit.");

            sumDebit += line.Debit;
            sumCredit += line.Credit;
        }

        TotalDebit = decimal.Round(sumDebit, 4, MidpointRounding.ToEven);
        TotalCredit = decimal.Round(sumCredit, 4, MidpointRounding.ToEven);

        if (TotalDebit != TotalCredit)
        {
            throw new InvalidOperationException(
                $"Double-entry invariant violated for entry '{EntryNumber}': TotalDebit ({TotalDebit:N4}) != TotalCredit ({TotalCredit:N4}). Difference: {TotalDebit - TotalCredit:N4}.");
        }
    }
}
