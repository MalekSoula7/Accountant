using System.Text;
using ComptaShield.Audit.Entities;
using ComptaShield.Audit.Services;
using ComptaShield.Inventory.Services;
using ComptaShield.LedgerAndBilan.Entities;
using ComptaShield.LedgerAndBilan.Persistence;
using ComptaShield.LedgerAndBilan.Services;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.CoreCalculations.Tests;

public sealed class GoldenDatasetTests
{
    private static readonly Guid CabinetId = Guid.NewGuid();
    private static readonly Guid DossierId = Guid.NewGuid();
    private static readonly Guid ExerciceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static (LedgerDbContext db, ScopedTenantContext tenant) CreateLedgerContext(string dbName)
    {
        var tenant = new ScopedTenantContext
        {
            UserId = UserId,
            CabinetId = CabinetId,
            DossierId = DossierId,
            ExerciceId = ExerciceId,
            Role = DossierRole.Partner,
            IsExerciceLocked = false
        };

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new LedgerDbContext(options, tenant), tenant);
    }

    // ---------------------------------------------------------------------------------
    // 1. GOLDEN DATASET: 1,000+ JOURNAL ENTRIES & ZERO ROUNDING DRIFT BILAN BALANCE
    // ---------------------------------------------------------------------------------
    [Fact]
    public async Task GoldenDataset_1000_Entries_Should_Have_Zero_Rounding_Drift_And_Balance_Actif_Equals_Passif()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, tenant) = CreateLedgerContext(dbName);

        // 1. Initial Equity Contribution (Capital 500,000 TND into Bank 512000)
        var initEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CabinetId = CabinetId,
            DossierId = DossierId,
            ExerciceId = ExerciceId,
            EntryNumber = "OD-2026-0001",
            JournalCode = "OD",
            EntryDate = new DateTime(2026, 1, 1),
            Description = "Constitution du capital social",
            Status = JournalEntryStatus.Posted,
            Lines =
            [
                new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "512000", AccountLabel = "Banque", Debit = 500000m, Credit = 0m, Description = "Apport en numéraire" },
                new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "101000", AccountLabel = "Capital social", Debit = 0m, Credit = 500000m, Description = "Capital souscrit" }
            ]
        };
        db.JournalEntries.Add(initEntry);

        // 2. Generate 1,000 high-precision business transactions (Purchases & Sales)
        // With varying tax rates (19%, 7%), discounts, and 4-decimal amounts
        var rng = new Random(42); // deterministic seed for reproducibility

        for (int i = 1; i <= 1000; i++)
        {
            var isSale = rng.NextDouble() > 0.45; // 55% sales, 45% purchases
            var baseAmount = decimal.Round((decimal)(rng.NextDouble() * 8500.0 + 150.0), 4, MidpointRounding.ToEven);
            var tvaRate = rng.NextDouble() > 0.3 ? 0.19m : 0.07m;
            var tvaAmount = decimal.Round(baseAmount * tvaRate, 4, MidpointRounding.ToEven);
            var timbre = 1.0000m;
            var totalTtc = decimal.Round(baseAmount + tvaAmount + timbre, 4, MidpointRounding.ToEven);

            JournalEntry entry;

            if (isSale)
            {
                // Vente: Debit Client 411000 (TTC) | Credit Ventes 707000 (HT), Credit TVA Collectée 436700, Credit Timbre 708000
                // Invariant: Debit == Credit
                entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    CabinetId = CabinetId,
                    DossierId = DossierId,
                    ExerciceId = ExerciceId,
                    EntryNumber = $"VT-2026-{i:D4}",
                    JournalCode = "VT",
                    EntryDate = new DateTime(2026, 1, 2).AddHours(i),
                    Description = $"Facture Vente #{i}",
                    DocumentId = Guid.NewGuid(),
                    Status = JournalEntryStatus.Posted,
                    Lines =
                    [
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "411000", AccountLabel = "Clients", Debit = totalTtc, Credit = 0m, Description = $"Créance Vente #{i}" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "707000", AccountLabel = "Ventes de marchandises", Debit = 0m, Credit = baseAmount, Description = $"Vente HT #{i}" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "436700", AccountLabel = "TVA Collectée", Debit = 0m, Credit = tvaAmount, Description = $"TVA #{i}" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "708000", AccountLabel = "Autres produits / Timbre", Debit = 0m, Credit = timbre, Description = "Droit de timbre" }
                    ]
                };
            }
            else
            {
                // Achat: Debit Achats 607000 (HT), Debit TVA Déductible 436600, Debit Charges timbre 624000 | Credit Fournisseur 401000 (TTC)
                // Invariant: Debit == Credit
                entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    CabinetId = CabinetId,
                    DossierId = DossierId,
                    ExerciceId = ExerciceId,
                    EntryNumber = $"HA-2026-{i:D4}",
                    JournalCode = "HA",
                    EntryDate = new DateTime(2026, 1, 2).AddHours(i),
                    Description = $"Facture Achat #{i}",
                    DocumentId = Guid.NewGuid(),
                    Status = JournalEntryStatus.Posted,
                    Lines =
                    [
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "607000", AccountLabel = "Achats de marchandises", Debit = baseAmount, Credit = 0m, Description = $"Achat HT #{i}" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "436600", AccountLabel = "TVA Déductible", Debit = tvaAmount, Credit = 0m, Description = $"TVA Déductible #{i}" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "624000", AccountLabel = "Frais de timbre", Debit = timbre, Credit = 0m, Description = "Droit de timbre" },
                        new JournalEntryLine { Id = Guid.NewGuid(), AccountNumber = "401000", AccountLabel = "Fournisseurs", Debit = 0m, Credit = totalTtc, Description = $"Dette Fournisseur #{i}" }
                    ]
                };
            }

            db.JournalEntries.Add(entry);
        }

        await db.SaveChangesAsync();

        var bilanService = new BilanCalculationService(db, tenant);

        // 1. Verify Trial Balance
        var trialBalance = await bilanService.GenerateTrialBalanceAsync();
        Assert.True(trialBalance.IsBalanced);
        Assert.Equal(trialBalance.GrandTotalDebit, trialBalance.GrandTotalCredit);
        Assert.Equal(trialBalance.GrandTotalSoldeDebiteur, trialBalance.GrandTotalSoldeCrediteur);

        // 2. Verify Bilan Actif vs Bilan Passif & Résultat
        var actif = await bilanService.GenerateBilanActifAsync();
        var passif = await bilanService.GenerateBilanPassifAsync();
        var pnl = await bilanService.GenerateEtatDeResultatAsync();

        // Fundamental Accounting Equation: Actif == Passif + Capitaux Propres (including Net Result)
        Assert.Equal(actif.TotalActif, passif.TotalPassif);
        Assert.Equal(0.0000m, Math.Abs(actif.TotalActif - passif.TotalPassif)); // Exact zero drift!

        // Verify drill-down entries exist
        Assert.NotEmpty(actif.Lines.First(l => l.Code == "ACT_CREANCES").DrillDownEntries);
        Assert.NotEmpty(passif.Lines.First(l => l.Code == "PAS_DETTES_CT").DrillDownEntries);
    }

    // ---------------------------------------------------------------------------------
    // 2. CUMP MULTI-STEP LOT VALUATION
    // ---------------------------------------------------------------------------------
    [Fact]
    public void CumpValuation_Should_Maintain_Exact_Precision_Through_Receipts_And_Issues()
    {
        var calculator = new CumpValuationCalculator();

        // Initial state: 0 units
        var state = new StockState(0m, 0m, 0m);

        // 1. Receipt 1: 100 units @ 10.00 TND -> Total 1,000.00 TND, CUMP = 10.0000
        var r1 = calculator.ProcessReceipt(state, 100m, 10m);
        Assert.Equal(100m, r1.NewState.TotalQuantity);
        Assert.Equal(10.0000m, r1.NewState.UnitCost);
        Assert.Equal(1000.0000m, r1.NewState.TotalValuation);

        // 2. Issue 1: 30 units -> Cost 30 * 10 = 300, Remaining 70 units @ 10.0000 = 700.00 TND
        var i1 = calculator.ProcessIssue(r1.NewState, 30m);
        Assert.Equal(70m, i1.NewState.TotalQuantity);
        Assert.Equal(10.0000m, i1.NewState.UnitCost);
        Assert.Equal(700.0000m, i1.NewState.TotalValuation);

        // 3. Receipt 2: 50 units @ 13.00 TND -> Total added = 650.00 TND
        // New Qty = 120 units, New Valuation = 700 + 650 = 1350.00 TND, New CUMP = 1350 / 120 = 11.2500
        var r2 = calculator.ProcessReceipt(i1.NewState, 50m, 13m);
        Assert.Equal(120m, r2.NewState.TotalQuantity);
        Assert.Equal(11.2500m, r2.NewState.UnitCost);
        Assert.Equal(1350.0000m, r2.NewState.TotalValuation);

        // 4. Issue 2: 80 units @ 11.2500 -> Cost = 900.00 TND, Remaining 40 units @ 11.2500 = 450.00 TND
        var i2 = calculator.ProcessIssue(r2.NewState, 80m);
        Assert.Equal(40m, i2.NewState.TotalQuantity);
        Assert.Equal(11.2500m, i2.NewState.UnitCost);
        Assert.Equal(450.0000m, i2.NewState.TotalValuation);
    }

    // ---------------------------------------------------------------------------------
    // 3. FIFO LOT CONSUMPTION QUEUE
    // ---------------------------------------------------------------------------------
    [Fact]
    public void FifoValuation_Should_Consume_Oldest_Lots_First()
    {
        var calculator = new FifoValuationCalculator();
        var lots = new List<FifoLot>();

        // Lot A: 100 units @ 10 TND (Date: Day 1)
        var lotAId = Guid.NewGuid();
        lots = calculator.ProcessReceipt(lots, lotAId, new DateTime(2026, 1, 1), 100m, 10m);

        // Lot B: 50 units @ 12 TND (Date: Day 2)
        var lotBId = Guid.NewGuid();
        lots = calculator.ProcessReceipt(lots, lotBId, new DateTime(2026, 1, 2), 50m, 12m);

        // Issue 120 units:
        // Consumes all 100 from Lot A (100 * 10 = 1000 TND) + 20 from Lot B (20 * 12 = 240 TND)
        // Total cost = 1240 TND. Remaining in Lot B = 30 units @ 12 = 360 TND.
        var issueResult = calculator.ProcessIssue(lots, 120m);

        Assert.Equal(120m, issueResult.TotalQuantityIssued);
        Assert.Equal(1240.0000m, issueResult.TotalCostOfIssue);
        Assert.Equal(2, issueResult.Consumptions.Count);

        Assert.Single(issueResult.RemainingLots);
        var remainingLot = issueResult.RemainingLots[0];
        Assert.Equal(lotBId, remainingLot.LotId);
        Assert.Equal(30m, remainingLot.Quantity);
        Assert.Equal(360.0000m, issueResult.RemainingTotalValuation);
    }

    // ---------------------------------------------------------------------------------
    // 4. STATUTORY AUDIT MATERIALITY & MONETARY UNIT SAMPLING
    // ---------------------------------------------------------------------------------
    [Fact]
    public void Materiality_And_Mus_Sampling_Should_Isolate_Top_Stratum_And_Provide_Coverage()
    {
        var materialityCalc = new MaterialityCalculator();
        // Annual revenue 10,000,000 TND
        var materiality = materialityCalc.Calculate(MaterialityBenchmark.Revenue, 10000000m, customPercentage: 0.015m); // 1.5%

        Assert.Equal(150000.0000m, materiality.PlanningMateriality);        // 150,000 TND
        Assert.Equal(112500.0000m, materiality.PerformanceMateriality);     // 75% = 112,500 TND
        Assert.Equal(7500.0000m, materiality.ClearlyTrivialThreshold);      // 5% = 7,500 TND

        var musSelector = new MonetaryUnitSamplingSelector();
        // Sampling interval = 112,500 / 3.0 = 37,500 TND
        var population = new List<PopulationItem>
        {
            new("INV-001", "Gros équipement", 85000m), // > 37,500 -> Top Stratum!
            new("INV-002", "Licence logicielle", 45000m), // > 37,500 -> Top Stratum!
            new("INV-003", "Prestation audit", 15000m),
            new("INV-004", "Fournitures bureau", 5000m),
            new("INV-005", "Maintenance IT", 25000m),
            new("INV-006", "Abonnement cloud", 18000m),
            new("INV-007", "Télécoms", 8000m)
        };

        var samplingResult = musSelector.SelectMusSample(population, materiality.PerformanceMateriality, reliabilityFactor: 3.0m);

        Assert.Equal(2, samplingResult.TopStratumItems.Count);
        Assert.Contains(samplingResult.TopStratumItems, i => i.Identifier == "INV-001");
        Assert.Contains(samplingResult.TopStratumItems, i => i.Identifier == "INV-002");
        Assert.True(samplingResult.CoveragePercentage > 50m);
    }

    // ---------------------------------------------------------------------------------
    // 5. SIGNED DELIVERABLE GENERATOR
    // ---------------------------------------------------------------------------------
    [Fact]
    public async Task SignedDeliverableGenerator_Should_Produce_Valid_Sha256_And_StorageKey()
    {
        var generator = new SignedDeliverableGenerator();
        var samplePdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Deliverable Bilan Actif 2026");
        using var stream = new MemoryStream(samplePdfBytes);

        var result = await generator.GenerateAndSignDeliverableAsync(
            CabinetId, DossierId, ExerciceId,
            "bilan_actif", "v1.0", stream, UserId);

        Assert.NotNull(result.Deliverable);
        Assert.NotEmpty(result.Sha256Hash);
        Assert.NotEmpty(result.HmacSignature);
        Assert.Contains("bilan_actif_v1.0", result.StorageKey);
        Assert.StartsWith($"{CabinetId}/{DossierId}/{ExerciceId}/deliverables/", result.StorageKey);
    }
}
