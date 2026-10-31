using ComptaShield.LedgerAndBilan.Entities;
using ComptaShield.LedgerAndBilan.Persistence;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.LedgerAndBilan.Services;

public sealed record DrillDownReference(
    Guid JournalEntryId,
    string EntryNumber,
    DateTime EntryDate,
    Guid? DocumentId,
    string Description,
    decimal Amount
);

public sealed record BilanLine(
    string Code,
    string Label,
    decimal Amount,
    List<string> AccountNumbers,
    List<DrillDownReference> DrillDownEntries
);

public sealed record BilanActifResult(
    decimal TotalActifImmobilise,
    decimal TotalActifCourant,
    decimal TotalTresorerieActif,
    decimal TotalActif,
    List<BilanLine> Lines
);

public sealed record BilanPassifResult(
    decimal TotalCapitauxPropres,
    decimal TotalPassifsNonCourants,
    decimal TotalPassifsCourants,
    decimal TotalTresoreriePassif,
    decimal TotalPassif,
    List<BilanLine> Lines
);

public sealed record EtatDeResultatResult(
    decimal TotalProduitsExploitation,
    decimal TotalChargesExploitation,
    decimal ResultatExploitation,
    decimal ResultatNet,
    List<BilanLine> Lines
);

public sealed record TrialBalanceAccount(
    string AccountNumber,
    string AccountLabel,
    int AccountClass,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal SoldeDebiteur,
    decimal SoldeCrediteur
);

public sealed record TrialBalanceResult(
    List<TrialBalanceAccount> Accounts,
    decimal GrandTotalDebit,
    decimal GrandTotalCredit,
    decimal GrandTotalSoldeDebiteur,
    decimal GrandTotalSoldeCrediteur,
    bool IsBalanced
);

/// <summary>
/// Core accounting calculation service generating Trial Balance (Balance Générale),
/// Bilan Actif, Bilan Passif, and État de Résultat with exact drill-down links to source
/// journal entries and original ingested document IDs.
/// Enforces strict decimal math with zero rounding drift.
/// </summary>
public sealed class BilanCalculationService
{
    private readonly LedgerDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public BilanCalculationService(LedgerDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Generates the Trial Balance (Balance Générale) across all accounts.
    /// Invariant: GrandTotalDebit == GrandTotalCredit && GrandTotalSoldeDebiteur == GrandTotalSoldeCrediteur.
    /// </summary>
    public async Task<TrialBalanceResult> GenerateTrialBalanceAsync(CancellationToken ct = default)
    {
        // Query posted/validated lines in the current exercice
        var lines = await _dbContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted 
                     || l.JournalEntry.Status == JournalEntryStatus.Validated)
            .ToListAsync(ct);

        var grouped = lines.GroupBy(l => l.AccountNumber).ToList();
        var accounts = new List<TrialBalanceAccount>();

        decimal grandDebit = 0m;
        decimal grandCredit = 0m;
        decimal grandSoldeDeb = 0m;
        decimal grandSoldeCred = 0m;

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            var accNum = group.Key;
            var accLabel = group.First().AccountLabel;
            var accClass = char.IsDigit(accNum[0]) ? (accNum[0] - '0') : 0;

            var sumDeb = decimal.Round(group.Sum(l => l.Debit), 4, MidpointRounding.ToEven);
            var sumCred = decimal.Round(group.Sum(l => l.Credit), 4, MidpointRounding.ToEven);

            decimal soldeDeb = 0m;
            decimal soldeCred = 0m;

            if (sumDeb >= sumCred)
            {
                soldeDeb = sumDeb - sumCred;
            }
            else
            {
                soldeCred = sumCred - sumDeb;
            }

            grandDebit += sumDeb;
            grandCredit += sumCred;
            grandSoldeDeb += soldeDeb;
            grandSoldeCred += soldeCred;

            accounts.Add(new TrialBalanceAccount(
                accNum, accLabel, accClass, sumDeb, sumCred, soldeDeb, soldeCred));
        }

        grandDebit = decimal.Round(grandDebit, 4, MidpointRounding.ToEven);
        grandCredit = decimal.Round(grandCredit, 4, MidpointRounding.ToEven);
        grandSoldeDeb = decimal.Round(grandSoldeDeb, 4, MidpointRounding.ToEven);
        grandSoldeCred = decimal.Round(grandSoldeCred, 4, MidpointRounding.ToEven);

        var isBalanced = (grandDebit == grandCredit) && (grandSoldeDeb == grandSoldeCred);

        return new TrialBalanceResult(
            accounts,
            grandDebit,
            grandCredit,
            grandSoldeDeb,
            grandSoldeCred,
            isBalanced
        );
    }

    /// <summary>
    /// Generates Bilan Actif (Assets) with full drill-down to journal entries and documents.
    /// </summary>
    public async Task<BilanActifResult> GenerateBilanActifAsync(CancellationToken ct = default)
    {
        var lines = await _dbContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted 
                     || l.JournalEntry.Status == JournalEntryStatus.Validated)
            .ToListAsync(ct);

        var bilanLines = new List<BilanLine>();

        // 1. Actif Immobilisé (Class 2: Accounts starting with '2')
        var immobLines = lines.Where(l => l.AccountNumber.StartsWith('2')).ToList();
        var immobDrillDown = ToDrillDown(immobLines);
        var immobBalance = decimal.Round(immobLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        bilanLines.Add(new BilanLine(
            "ACT_IMMOB", "Actifs non courants / Immobilisations", immobBalance,
            immobLines.Select(l => l.AccountNumber).Distinct().ToList(), immobDrillDown));

        // 2. Stocks (Class 3: Accounts starting with '3')
        var stockLines = lines.Where(l => l.AccountNumber.StartsWith('3')).ToList();
        var stockBalance = decimal.Round(stockLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        bilanLines.Add(new BilanLine(
            "ACT_STOCKS", "Stocks et en-cours", stockBalance,
            stockLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(stockLines)));

        // 3. Créances Clients et comptes rattachés (Class 4 debit balances: e.g. 411, 416, 436)
        var clientLines = lines.Where(l => l.AccountNumber.StartsWith("41") || l.AccountNumber.StartsWith("436") || l.AccountNumber.StartsWith("445")).ToList();
        var clientBalance = decimal.Round(clientLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        // Only include positive (debit) receivable balance
        var clientActif = Math.Max(0m, clientBalance);
        bilanLines.Add(new BilanLine(
            "ACT_CREANCES", "Clients et autres créances courantes", clientActif,
            clientLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(clientLines)));

        // 4. Trésorerie Actif (Class 5 debit balances: 512, 532, 54)
        var tresoLines = lines.Where(l => l.AccountNumber.StartsWith('5')).ToList();
        var tresoBalance = decimal.Round(tresoLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        var tresoActif = Math.Max(0m, tresoBalance);
        bilanLines.Add(new BilanLine(
            "ACT_TRESO", "Trésorerie Actif (Banques & Caisses)", tresoActif,
            tresoLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(tresoLines)));

        var totalImmobilise = immobBalance;
        var totalCourant = stockBalance + clientActif;
        var totalTresorerie = tresoActif;
        var totalActif = decimal.Round(totalImmobilise + totalCourant + totalTresorerie, 4, MidpointRounding.ToEven);

        return new BilanActifResult(
            totalImmobilise,
            totalCourant,
            totalTresorerie,
            totalActif,
            bilanLines
        );
    }

    /// <summary>
    /// Generates Bilan Passif & Capitaux Propres (Liabilities & Equity).
    /// </summary>
    public async Task<BilanPassifResult> GenerateBilanPassifAsync(CancellationToken ct = default)
    {
        var lines = await _dbContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted 
                     || l.JournalEntry.Status == JournalEntryStatus.Validated)
            .ToListAsync(ct);

        var bilanLines = new List<BilanLine>();

        // 1. Capitaux Propres (Class 1 Equity accounts: 101, 106, 110, etc.)
        var equityLines = lines.Where(l => l.AccountNumber.StartsWith("10") || l.AccountNumber.StartsWith("11")).ToList();
        var equityBalance = decimal.Round(equityLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        bilanLines.Add(new BilanLine(
            "PAS_EQUITY", "Capital et réserves", equityBalance,
            equityLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(equityLines)));

        // 2. Résultat de l'exercice (Class 7 - Class 6)
        var chargeLines = lines.Where(l => l.AccountNumber.StartsWith('6')).ToList();
        var produitLines = lines.Where(l => l.AccountNumber.StartsWith('7')).ToList();
        var totalCharges = decimal.Round(chargeLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        var totalProduits = decimal.Round(produitLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        var resultatExercice = decimal.Round(totalProduits - totalCharges, 4, MidpointRounding.ToEven);

        bilanLines.Add(new BilanLine(
            "PAS_RESULTAT", "Résultat net de l'exercice", resultatExercice,
            ["7*", "6*"], []));

        // 3. Passifs Non Courants (Class 1 debt accounts: 16*)
        var longTermDebtLines = lines.Where(l => l.AccountNumber.StartsWith("16")).ToList();
        var longTermDebt = decimal.Round(longTermDebtLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        bilanLines.Add(new BilanLine(
            "PAS_DETTES_LT", "Emprunts et dettes financières non courantes", longTermDebt,
            longTermDebtLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(longTermDebtLines)));

        // 4. Passifs Courants (Fournisseurs 401, Dettes fiscales et sociales 42*, 43*, 44*)
        var supplierLines = lines.Where(l => l.AccountNumber.StartsWith("401") || l.AccountNumber.StartsWith("42") || l.AccountNumber.StartsWith("437") || l.AccountNumber.StartsWith("447")).ToList();
        var supplierBalance = decimal.Round(supplierLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        var supplierPassif = Math.Max(0m, supplierBalance);
        bilanLines.Add(new BilanLine(
            "PAS_DETTES_CT", "Fournisseurs et autres dettes courantes", supplierPassif,
            supplierLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(supplierLines)));

        // 5. Trésorerie Passif (Overdrafts: Class 5 credit balances)
        var tresoLines = lines.Where(l => l.AccountNumber.StartsWith('5')).ToList();
        var tresoCredit = decimal.Round(tresoLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        var tresoPassif = Math.Max(0m, tresoCredit);
        bilanLines.Add(new BilanLine(
            "PAS_TRESO", "Trésorerie Passif (Concours bancaires courants)", tresoPassif,
            tresoLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(tresoLines)));

        var totalCapitauxPropres = decimal.Round(equityBalance + resultatExercice, 4, MidpointRounding.ToEven);
        var totalPassifsNonCourants = longTermDebt;
        var totalPassifsCourants = supplierPassif;
        var totalTresoreriePassif = tresoPassif;

        var totalPassif = decimal.Round(
            totalCapitauxPropres + totalPassifsNonCourants + totalPassifsCourants + totalTresoreriePassif,
            4, MidpointRounding.ToEven);

        return new BilanPassifResult(
            totalCapitauxPropres,
            totalPassifsNonCourants,
            totalPassifsCourants,
            totalTresoreriePassif,
            totalPassif,
            bilanLines
        );
    }

    /// <summary>
    /// Generates the Income Statement (État de Résultat).
    /// </summary>
    public async Task<EtatDeResultatResult> GenerateEtatDeResultatAsync(CancellationToken ct = default)
    {
        var lines = await _dbContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted 
                     || l.JournalEntry.Status == JournalEntryStatus.Validated)
            .ToListAsync(ct);

        var chargeLines = lines.Where(l => l.AccountNumber.StartsWith('6')).ToList();
        var produitLines = lines.Where(l => l.AccountNumber.StartsWith('7')).ToList();

        var totalCharges = decimal.Round(chargeLines.Sum(l => l.Debit - l.Credit), 4, MidpointRounding.ToEven);
        var totalProduits = decimal.Round(produitLines.Sum(l => l.Credit - l.Debit), 4, MidpointRounding.ToEven);
        var resultatNet = decimal.Round(totalProduits - totalCharges, 4, MidpointRounding.ToEven);

        var bilanLines = new List<BilanLine>
        {
            new("PROD_EXPL", "Produits d'exploitation", totalProduits, 
                produitLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(produitLines)),
            new("CHG_EXPL", "Charges d'exploitation", totalCharges, 
                chargeLines.Select(l => l.AccountNumber).Distinct().ToList(), ToDrillDown(chargeLines)),
            new("RES_NET", "Résultat net de l'exercice", resultatNet, ["6*", "7*"], [])
        };

        return new EtatDeResultatResult(
            totalProduits,
            totalCharges,
            resultatNet,
            resultatNet,
            bilanLines
        );
    }

    private static List<DrillDownReference> ToDrillDown(List<JournalEntryLine> lines)
    {
        return lines.Select(l => new DrillDownReference(
            l.JournalEntryId,
            l.JournalEntry.EntryNumber,
            l.JournalEntry.EntryDate,
            l.JournalEntry.DocumentId,
            $"{l.AccountNumber} - {l.Description}",
            l.Debit > 0 ? l.Debit : -l.Credit
        )).ToList();
    }
}
