using System.Text;
using ComptaShield.FraudShield.Entities;
using ComptaShield.FraudShield.Persistence;
using ComptaShield.FraudShield.Services;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.FraudShield.UnitTests;

public sealed class FraudShieldUnitTests
{
    private static readonly Guid CabinetId = Guid.NewGuid();
    private static readonly Guid DossierId = Guid.NewGuid();
    private static readonly Guid CurrentExerciceId = Guid.NewGuid();
    private static readonly Guid PriorExerciceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static (FraudDbContext db, ScopedTenantContext tenant) CreateContext(
        string dbName, 
        Guid? exerciceId = null)
    {
        var tenant = new ScopedTenantContext
        {
            UserId = UserId,
            CabinetId = CabinetId,
            DossierId = DossierId,
            ExerciceId = exerciceId ?? CurrentExerciceId,
            Role = DossierRole.Partner,
            IsExerciceLocked = false
        };

        var options = new DbContextOptionsBuilder<FraudDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new FraudDbContext(options, tenant), tenant);
    }

    // ---------------------------------------------------------
    // 1. PDF METADATA FORENSICS
    // ---------------------------------------------------------
    [Fact]
    public void PdfForensicsScanner_Should_Flag_Photoshop_Tampered_Metadata()
    {
        var scanner = new PdfMetadataForensicsScanner();

        // Synthetic minimal PDF stream with Photoshop metadata
        var fakePdfContent = """
            %PDF-1.4
            1 0 obj << /Title (Facture) /Creator (Adobe Photoshop 2024) /Producer (iLovePDF Tools) /CreationDate (D:20260101100000) /ModDate (D:20260105150000) >> endobj
            2 0 obj << /Type /Catalog /Pages 3 0 R >> endobj
            3 0 obj << /Type /Pages /Kids [4 0 R] /Count 1 >> endobj
            4 0 obj << /Type /Page /Parent 3 0 R /MediaBox [0 0 612 792] >> endobj
            xref
            0 5
            0000000000 65535 f 
            0000000009 00000 n 
            0000000150 00000 n 
            0000000200 00000 n 
            0000000260 00000 n 
            trailer << /Size 5 /Root 2 0 R /Info 1 0 R >>
            startxref
            340
            %%EOF
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fakePdfContent));
        var report = scanner.Scan(stream);

        Assert.True(report.IsSuspicious);
        Assert.True(report.RiskScore >= 0.85m);
        Assert.Contains(report.DetectedSoftwareAnomalies, a => a.Contains("photoshop") || a.Contains("ilovepdf"));
    }

    // ---------------------------------------------------------
    // 2. SEMANTIC & FUZZY DUPLICATE DETECTOR
    // ---------------------------------------------------------
    [Fact]
    public async Task SemanticDuplicateDetector_Should_Flag_Fuzzy_And_CrossExercice_Duplicate()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed an invoice from the PRIOR fiscal year using a context scoped to PriorExerciceId
        var priorDocId = Guid.NewGuid();
        {
            var (seedDb, _) = CreateContext(dbName, PriorExerciceId);
            seedDb.Fingerprints.Add(new IndexedInvoiceFingerprint
            {
                Id = Guid.NewGuid(),
                CabinetId = CabinetId,
                DossierId = DossierId,
                ExerciceId = PriorExerciceId,
                DocumentId = priorDocId,
                SupplierMatriculeFiscal = "1234567/B/M/000",
                OriginalInvoiceNumber = "FAC-2025-0042",
                NormalizedInvoiceNumber = "20250042",
                InvoiceDate = new DateTime(2025, 12, 28),
                TotalTTC = 12500.0000m,
                PerceptualHash = 0xAAFFAAFFAAFFAAFF
            });
            await seedDb.SaveChangesAsync();
            await seedDb.DisposeAsync();
        }

        // Query using a context scoped to CURRENT fiscal year
        var (db, tenant) = CreateContext(dbName, CurrentExerciceId);
        var detector = new SemanticDuplicateDetector(db, tenant);

        // New inbound invoice in CURRENT fiscal year with slightly formatted invoice number and near-identical pHash
        var request = new DuplicateCheckRequest(
            CurrentDocumentId: Guid.NewGuid(),
            SupplierMatriculeFiscal: "1234567-B-M-000", // hyphenated variant
            InvoiceNumber: "FAC/2025/0042",             // slash variant
            InvoiceDate: new DateTime(2025, 12, 28),
            TotalTTC: 12500.0000m,
            PerceptualHash: 0xAAFFAAFFAAFFAAFDul       // Hamming distance = 1 bit!
        );

        var result = await detector.CheckDuplicateAsync(request);

        Assert.True(result.IsDuplicate);
        Assert.NotNull(result.BestMatch);
        Assert.True(result.BestMatch.IsCrossExercice);
        Assert.Equal(priorDocId, result.BestMatch.MatchedDocumentId);
        Assert.True(result.BestMatch.HammingDistance <= 5);
        Assert.True(result.HighestConfidence >= 0.75m);
    }

    // ---------------------------------------------------------
    // 3. THREE-WAY MATCHING ENGINE
    // ---------------------------------------------------------
    [Fact]
    public void ThreeWayMatchingEngine_Should_Flag_Phantom_Delivery_And_Price_Inflation()
    {
        var engine = new ThreeWayMatchingEngine();

        // Approved Purchase Order
        var po = new PurchaseOrder(
            "BC-2026-001",
            new DateTime(2026, 1, 10),
            "1234567/A/M/000",
            [
                new PurchaseOrderLine("SRV-01", "Serveur Dell", 5m, 1000m)
            ]
        );

        // Actual Delivery Note (BL): only 3 delivered physically!
        var bl = new DeliveryNote(
            "BL-2026-001",
            "BC-2026-001",
            new DateTime(2026, 1, 15),
            [
                new DeliveryNoteLine("SRV-01", 3m) // 2 units short!
            ]
        );

        // Invoice claims 5 delivered AND inflates unit price to 1100 TND!
        var invoice = new InvoiceMatchInput(
            "FAC-2026-999",
            "BC-2026-001",
            "BL-2026-001",
            "1234567/A/M/000",
            new DateTime(2026, 1, 16),
            TotalHT: 5500m,
            TotalTTC: 6545m,
            Lines:
            [
                new InvoiceMatchLine("SRV-01", 5m, 1100m)
            ]
        );

        var result = engine.Reconcile(po, bl, invoice);

        Assert.False(result.IsFullMatch);
        Assert.True(result.RiskScore >= 0.85m);
        Assert.Contains(result.Discrepancies, d => d.Type == MatchDiscrepancyType.PhantomDelivery);
        Assert.Contains(result.Discrepancies, d => d.Type == MatchDiscrepancyType.UnitPriceInflation);
        Assert.True(result.TotalFinancialImpact > 0m);
    }

    // ---------------------------------------------------------
    // 4. BENFORD'S LAW & THRESHOLD SPLITTING
    // ---------------------------------------------------------
    [Fact]
    public void BenfordLawAuditService_Should_Detect_Fabricated_Uniform_Numbers()
    {
        var service = new BenfordLawAuditService();

        // Synthetic non-Benford dataset: 900 amounts uniformly distributed across digits 1 to 9 (100 each)
        // Benford's law expects ~30% ones, but here we force exactly 11% for every digit.
        var fabricatedAmounts = new List<decimal>();
        for (int digit = 1; digit <= 9; digit++)
        {
            for (int i = 0; i < 100; i++)
            {
                fabricatedAmounts.Add((decimal)(digit * 1000 + i * 5));
            }
        }

        var report = service.Analyze(fabricatedAmounts);

        Assert.True(report.BenfordResult.AnomalyDetected);
        Assert.True(report.BenfordResult.ChiSquareStatistic > BenfordLawAuditService.ChiSquareCriticalValue95);
        Assert.True(report.RequiresAuditorInvestigation);
    }

    [Fact]
    public void BenfordLawAuditService_Should_Detect_Threshold_Splitting_Clusters()
    {
        var service = new BenfordLawAuditService();

        // Normal base transactions + cluster of 6 invoices placed intentionally at 4950, 4980, 4990
        // to evade a 5,000 TND manager signature threshold.
        var amounts = new List<decimal>
        {
            120m, 350m, 800m, 1200m, 2500m, 3100m, 7200m, 15000m,
            4950m, 4960m, 4975m, 4980m, 4990m, 4995m // Smurfing cluster!
        };

        var report = service.Analyze(amounts, [5000m]);

        Assert.Contains(report.ThresholdSplittingClusters, c => c.Threshold == 5000m && c.TransactionCount >= 5);
        Assert.True(report.RequiresAuditorInvestigation);
    }

    // ---------------------------------------------------------
    // 5. FRAUD RECTIFICATION & REVERSING ENTRY
    // ---------------------------------------------------------
    [Fact]
    public async Task FraudRectificationService_Should_Create_Balanced_Contrepassation_And_Ticket()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, tenant) = CreateContext(dbName);

        var alertId = Guid.NewGuid();
        db.Alerts.Add(new FraudAlert
        {
            Id = alertId,
            CabinetId = CabinetId,
            DossierId = DossierId,
            ExerciceId = CurrentExerciceId,
            Title = "Facture suspecte",
            Status = FraudAlertStatus.Active,
            RiskScore = 0.90m
        });
        await db.SaveChangesAsync();

        var service = new FraudRectificationService(db, tenant);

        // 1. Generate Client Clarification Ticket
        var ticket = await service.GenerateClientClarificationTicketAsync(
            alertId, "Veuillez fournir le bon de livraison signé pour la facture FAC-999.");

        Assert.NotNull(ticket);
        Assert.NotEmpty(ticket.MagicToken);
        Assert.Equal(ClarificationTicketStatus.Pending, ticket.Status);

        // Verify alert status updated to ClarificationRequested
        var updatedAlert = await db.Alerts.FindAsync(alertId);
        Assert.Equal(FraudAlertStatus.ClarificationRequested, updatedAlert!.Status);

        // 2. Quarantine Document
        var quarantined = await service.QuarantineDocumentAsync(alertId, "Fournisseur fantôme confirmé par appel");
        Assert.True(quarantined);

        // 3. Generate Reversing Entry (Contrepassation)
        var originalLines = new List<JournalLineDto>
        {
            new("607000", "Achats de marchandises", 1000m, 0m),
            new("436600", "TVA Déductible", 190m, 0m),
            new("401000", "Fournisseurs", 0m, 1190m)
        };

        var originalEntryId = Guid.NewGuid();
        var proposal = service.CreateReversingEntryProposal(
            originalEntryId, "ECR-2026-0412", originalLines, "Annulation facture frauduleuse");

        // Verify reversing properties:
        Assert.Equal(originalEntryId, proposal.ReversesEntryId);
        Assert.Equal(1190m, proposal.TotalDebit);
        Assert.Equal(1190m, proposal.TotalCredit);
        Assert.Equal(proposal.TotalDebit, proposal.TotalCredit); // Perfect double-entry balance

        // Verify swapped lines:
        var revSupplier = proposal.Lines.First(l => l.AccountNumber == "401000");
        Assert.Equal(1190m, revSupplier.Debit); // Original had Credit 1190
        Assert.Equal(0m, revSupplier.Credit);
    }
}
