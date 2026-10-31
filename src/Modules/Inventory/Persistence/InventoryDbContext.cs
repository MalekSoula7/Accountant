using ComptaShield.Inventory.Entities;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Inventory.Persistence;

public sealed class InventoryDbContext : ScopedModuleDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PhysicalInventoryCount> PhysicalInventoryCounts => Set<PhysicalInventoryCount>();
    public DbSet<PhysicalInventoryLine> PhysicalInventoryLines => Set<PhysicalInventoryLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("inventory");

        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("stock_items");
            e.HasKey(s => s.Id);
            e.Property(s => s.ItemCode).HasMaxLength(50).IsRequired();
            e.Property(s => s.Description).HasMaxLength(200).IsRequired();
            e.Property(s => s.Unit).HasMaxLength(20).IsRequired();
            e.Property(s => s.ValuationMethod).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.TheoreticalQuantity).HasColumnType("numeric(19,4)");
            e.Property(s => s.CurrentUnitCost).HasColumnType("numeric(19,4)");
            e.Property(s => s.TotalValuation).HasColumnType("numeric(19,4)");

            e.HasIndex(s => new { s.CabinetId, s.DossierId, s.ExerciceId, s.ItemCode }).IsUnique();
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("stock_movements");
            e.HasKey(m => m.Id);
            e.Property(m => m.MovementType).HasConversion<string>().HasMaxLength(30);
            e.Property(m => m.Quantity).HasColumnType("numeric(19,4)");
            e.Property(m => m.UnitPrice).HasColumnType("numeric(19,4)");
            e.Property(m => m.TotalAmount).HasColumnType("numeric(19,4)");
            e.Property(m => m.RunningStockQuantity).HasColumnType("numeric(19,4)");
            e.Property(m => m.RunningCump).HasColumnType("numeric(19,4)");
            e.Property(m => m.Reference).HasMaxLength(100);

            e.HasOne(m => m.StockItem)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.StockItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => new { m.CabinetId, m.DossierId, m.ExerciceId, m.StockItemId });
        });

        modelBuilder.Entity<PhysicalInventoryCount>(e =>
        {
            e.ToTable("physical_inventory_counts");
            e.HasKey(c => c.Id);
            e.Property(c => c.CountReference).HasMaxLength(100).IsRequired();
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.TotalTheoreticalValuation).HasColumnType("numeric(19,4)");
            e.Property(c => c.TotalPhysicalValuation).HasColumnType("numeric(19,4)");
            e.Property(c => c.TotalDiscrepancyAmount).HasColumnType("numeric(19,4)");
            e.Property(c => c.TotalDepreciationProvision).HasColumnType("numeric(19,4)");

            e.HasIndex(c => new { c.CabinetId, c.DossierId, c.ExerciceId, c.CountReference }).IsUnique();
        });

        modelBuilder.Entity<PhysicalInventoryLine>(e =>
        {
            e.ToTable("physical_inventory_lines");
            e.HasKey(l => l.Id);
            e.Property(l => l.ItemCode).HasMaxLength(50).IsRequired();
            e.Property(l => l.Description).HasMaxLength(200);
            e.Property(l => l.TheoreticalQuantity).HasColumnType("numeric(19,4)");
            e.Property(l => l.PhysicalQuantity).HasColumnType("numeric(19,4)");
            e.Property(l => l.DiscrepancyQuantity).HasColumnType("numeric(19,4)");
            e.Property(l => l.UnitCost).HasColumnType("numeric(19,4)");
            e.Property(l => l.FinancialDiscrepancy).HasColumnType("numeric(19,4)");
            e.Property(l => l.DepreciationProvision).HasColumnType("numeric(19,4)");
            e.Property(l => l.Notes).HasMaxLength(500);

            e.HasOne(l => l.PhysicalInventoryCount)
                .WithMany(c => c.Lines)
                .HasForeignKey(l => l.PhysicalInventoryCountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
