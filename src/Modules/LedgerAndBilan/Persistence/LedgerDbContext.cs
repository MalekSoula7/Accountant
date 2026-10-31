using ComptaShield.LedgerAndBilan.Entities;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.LedgerAndBilan.Persistence;

public sealed class LedgerDbContext : ScopedModuleDbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("ledger");

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.AccountNumber).HasMaxLength(20).IsRequired();
            e.Property(a => a.Label).HasMaxLength(200).IsRequired();

            e.HasIndex(a => new { a.CabinetId, a.DossierId, a.ExerciceId, a.AccountNumber }).IsUnique();
        });

        modelBuilder.Entity<JournalEntry>(e =>
        {
            e.ToTable("journal_entries");
            e.HasKey(j => j.Id);
            e.Property(j => j.EntryNumber).HasMaxLength(50).IsRequired();
            e.Property(j => j.JournalCode).HasMaxLength(10).IsRequired();
            e.Property(j => j.Reference).HasMaxLength(100);
            e.Property(j => j.Description).HasMaxLength(500);
            e.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(j => j.TotalDebit).HasColumnType("numeric(19,4)");
            e.Property(j => j.TotalCredit).HasColumnType("numeric(19,4)");

            e.HasIndex(j => new { j.CabinetId, j.DossierId, j.ExerciceId, j.EntryNumber }).IsUnique();
            e.HasIndex(j => new { j.CabinetId, j.DossierId, j.DocumentId });
        });

        modelBuilder.Entity<JournalEntryLine>(e =>
        {
            e.ToTable("journal_entry_lines");
            e.HasKey(l => l.Id);
            e.Property(l => l.AccountNumber).HasMaxLength(20).IsRequired();
            e.Property(l => l.AccountLabel).HasMaxLength(200);
            e.Property(l => l.Description).HasMaxLength(500);
            e.Property(l => l.Debit).HasColumnType("numeric(19,4)");
            e.Property(l => l.Credit).HasColumnType("numeric(19,4)");
            e.Property(l => l.LetteringCode).HasMaxLength(10);

            e.HasOne(l => l.JournalEntry)
                .WithMany(j => j.Lines)
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(l => new { l.CabinetId, l.DossierId, l.ExerciceId, l.AccountNumber });
        });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceLedgerInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceLedgerInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void EnforceLedgerInvariants()
    {
        // 1. Enforce append-only ledger for Posted/Validated entries
        foreach (var entry in ChangeTracker.Entries<JournalEntry>())
        {
            if (entry.State == EntityState.Modified)
            {
                var originalStatus = entry.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status));
                if (originalStatus is JournalEntryStatus.Posted or JournalEntryStatus.Validated)
                {
                    // If already posted, only status transition to Validated or setting ReversesEntryId is permitted
                    var modifiedProperties = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
                    var forbidden = modifiedProperties.Except([nameof(JournalEntry.Status), nameof(JournalEntry.ReversesEntryId)]).ToList();

                    if (forbidden.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"Mutation of posted journal entry '{entry.Entity.EntryNumber}' is forbidden by ledger immutability invariant. Modifications: {string.Join(", ", forbidden)}.");
                    }
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                var originalStatus = entry.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status));
                if (originalStatus is JournalEntryStatus.Posted or JournalEntryStatus.Validated)
                {
                    throw new InvalidOperationException(
                        $"Deletion of posted journal entry '{entry.Entity.EntryNumber}' is strictly forbidden by ledger immutability invariant.");
                }
            }
            else if (entry.State == EntityState.Added)
            {
                // Validate double-entry balance before saving
                entry.Entity.ValidateDoubleEntryBalance();
            }
        }
    }
}
