using ComptaShield.Inventory.Entities;
using ComptaShield.Inventory.Persistence;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Inventory.Services;

public sealed record PhysicalCountItemInput(
    Guid StockItemId,
    decimal PhysicalQuantity,
    decimal DepreciationProvision = 0m,
    string? Notes = null
);

public sealed record ReconciliationAnomaly(
    string ItemCode,
    string Description,
    decimal TheoreticalQuantity,
    decimal PhysicalQuantity,
    decimal DiscrepancyQuantity,
    decimal FinancialImpact,
    decimal DiscrepancyPercentage
);

public sealed record InventoryReconciliationReport(
    Guid CountId,
    string CountReference,
    DateTime CountDate,
    decimal TotalTheoreticalValuation,
    decimal TotalPhysicalValuation,
    decimal NetFinancialDiscrepancy,
    decimal TotalDepreciationProvision,
    List<ReconciliationAnomaly> ShrinkageAnomalies,
    bool RequiresAuditorInvestigation
);

/// <summary>
/// Reconciles theoretical warehouse inventory vs physical counts (PV d'Inventaire).
/// Calculates shrinkage / surplus anomalies, applies depreciation provisions,
/// and updates inventory ledger balances.
/// </summary>
public sealed class InventoryReconciliationService
{
    private readonly InventoryDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public InventoryReconciliationService(InventoryDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<InventoryReconciliationReport> ReconcileAndValidateCountAsync(
        string countReference,
        DateTime countDate,
        IReadOnlyList<PhysicalCountItemInput> counts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var count = new PhysicalInventoryCount
        {
            Id = Guid.NewGuid(),
            CountReference = countReference,
            CountDate = countDate,
            Status = InventoryCountStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        var stockItemIds = counts.Select(c => c.StockItemId).Distinct().ToList();
        var stockItems = await _dbContext.StockItems
            .Where(s => stockItemIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        decimal totalTheo = 0m;
        decimal totalPhys = 0m;
        decimal totalDiscrepancy = 0m;
        decimal totalProvision = 0m;
        var anomalies = new List<ReconciliationAnomaly>();

        foreach (var countInput in counts)
        {
            if (!stockItems.TryGetValue(countInput.StockItemId, out var stockItem))
            {
                throw new InvalidOperationException($"Stock item {countInput.StockItemId} not found.");
            }

            var theoreticalQty = stockItem.TheoreticalQuantity;
            var physicalQty = countInput.PhysicalQuantity;
            var unitCost = stockItem.CurrentUnitCost;

            var discrepancyQty = physicalQty - theoreticalQty;
            var financialDiscrepancy = decimal.Round(discrepancyQty * unitCost, 4, MidpointRounding.ToEven);
            var theoValuation = decimal.Round(theoreticalQty * unitCost, 4, MidpointRounding.ToEven);
            var physValuation = decimal.Round(physicalQty * unitCost, 4, MidpointRounding.ToEven);

            totalTheo += theoValuation;
            totalPhys += physValuation;
            totalDiscrepancy += financialDiscrepancy;
            totalProvision += countInput.DepreciationProvision;

            // Check for shrinkage anomaly (> 5% discrepancy or > 500 TND financial impact)
            var discrepancyPct = theoreticalQty > 0m 
                ? Math.Abs(discrepancyQty) / theoreticalQty 
                : (physicalQty > 0m ? 1m : 0m);

            if (Math.Abs(financialDiscrepancy) >= 500m || discrepancyPct >= 0.05m)
            {
                anomalies.Add(new ReconciliationAnomaly(
                    stockItem.ItemCode,
                    stockItem.Description,
                    theoreticalQty,
                    physicalQty,
                    discrepancyQty,
                    financialDiscrepancy,
                    decimal.Round(discrepancyPct * 100m, 2, MidpointRounding.ToEven)
                ));
            }

            var line = new PhysicalInventoryLine
            {
                Id = Guid.NewGuid(),
                PhysicalInventoryCountId = count.Id,
                StockItemId = stockItem.Id,
                ItemCode = stockItem.ItemCode,
                Description = stockItem.Description,
                TheoreticalQuantity = theoreticalQty,
                PhysicalQuantity = physicalQty,
                DiscrepancyQuantity = discrepancyQty,
                UnitCost = unitCost,
                FinancialDiscrepancy = financialDiscrepancy,
                DepreciationProvision = countInput.DepreciationProvision,
                Notes = countInput.Notes
            };

            count.Lines.Add(line);

            // Update StockItem quantities to match validated physical count
            stockItem.TheoreticalQuantity = physicalQty;
            stockItem.TotalValuation = decimal.Round(physicalQty * unitCost, 4, MidpointRounding.ToEven);

            // Record adjustment movement
            if (discrepancyQty != 0m)
            {
                var adjustmentMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    StockItemId = stockItem.Id,
                    MovementType = StockMovementType.PhysicalAdjustment,
                    MovementDate = countDate,
                    Quantity = discrepancyQty,
                    UnitPrice = unitCost,
                    TotalAmount = financialDiscrepancy,
                    RunningStockQuantity = physicalQty,
                    RunningCump = unitCost,
                    Reference = $"PV-INV:{countReference}"
                };

                _dbContext.StockMovements.Add(adjustmentMovement);
            }
        }

        count.TotalTheoreticalValuation = decimal.Round(totalTheo, 4, MidpointRounding.ToEven);
        count.TotalPhysicalValuation = decimal.Round(totalPhys, 4, MidpointRounding.ToEven);
        count.TotalDiscrepancyAmount = decimal.Round(totalDiscrepancy, 4, MidpointRounding.ToEven);
        count.TotalDepreciationProvision = decimal.Round(totalProvision, 4, MidpointRounding.ToEven);
        count.Status = InventoryCountStatus.Validated;
        count.ValidatedAt = DateTime.UtcNow;
        count.ValidatedByUserId = _tenantContext.UserId;

        _dbContext.PhysicalInventoryCounts.Add(count);
        await _dbContext.SaveChangesAsync(ct);

        return new InventoryReconciliationReport(
            count.Id,
            count.CountReference,
            count.CountDate,
            count.TotalTheoreticalValuation,
            count.TotalPhysicalValuation,
            count.TotalDiscrepancyAmount,
            count.TotalDepreciationProvision,
            anomalies,
            anomalies.Count > 0
        );
    }
}
