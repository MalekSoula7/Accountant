using System.Numerics;
using System.Text.RegularExpressions;
using ComptaShield.FraudShield.Entities;
using ComptaShield.FraudShield.Persistence;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.FraudShield.Services;

public sealed record DuplicateCheckRequest(
    Guid CurrentDocumentId,
    string SupplierMatriculeFiscal,
    string InvoiceNumber,
    DateTime InvoiceDate,
    decimal TotalTTC,
    ulong PerceptualHash = 0
);

public sealed record DuplicateMatch(
    Guid MatchedDocumentId,
    Guid MatchedExerciceId,
    bool IsCrossExercice,
    decimal ConfidenceScore,
    int HammingDistance,
    List<string> MatchReasons
);

public sealed record DuplicateCheckResult(
    bool IsDuplicate,
    decimal HighestConfidence,
    DuplicateMatch? BestMatch,
    List<DuplicateMatch> AllMatches
);

/// <summary>
/// Detects semantic and visual duplicate invoices across current and prior fiscal years (Exercices)
/// within the same client company (Dossier).
/// Combines:
/// 1. Normalized invoice number matching (Levenshtein & prefix removal)
/// 2. Supplier matricule fiscal & total TTC exact/fuzzy matching
/// 3. Perceptual Hash (pHash) Hamming distance comparison
/// </summary>
public sealed class SemanticDuplicateDetector
{
    private readonly FraudDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public SemanticDuplicateDetector(FraudDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<DuplicateCheckResult> CheckDuplicateAsync(
        DuplicateCheckRequest request, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedReqNumber = NormalizeInvoiceNumber(request.InvoiceNumber);
        var normalizedMatricule = NormalizeMatricule(request.SupplierMatriculeFiscal);

        // Query across current and prior exercices within the same Cabinet and Dossier
        var candidates = await _dbContext.Fingerprints
            .IgnoreQueryFilters()
            .Where(f => f.CabinetId == _tenantContext.CabinetId 
                     && f.DossierId == _tenantContext.DossierId
                     && f.DocumentId != request.CurrentDocumentId)
            .ToListAsync(ct);

        var matches = new List<DuplicateMatch>();

        foreach (var candidate in candidates)
        {
            var reasons = new List<string>();
            decimal score = 0m;

            var candNormMatricule = NormalizeMatricule(candidate.SupplierMatriculeFiscal);
            var candNormNumber = candidate.NormalizedInvoiceNumber;

            // 1. Supplier Match
            var sameSupplier = string.Equals(normalizedMatricule, candNormMatricule, StringComparison.OrdinalIgnoreCase);
            if (sameSupplier && !string.IsNullOrEmpty(normalizedMatricule))
            {
                score += 0.25m;
                reasons.Add($"Identical supplier matricule fiscal: '{candidate.SupplierMatriculeFiscal}'");
            }

            // 2. Amount Match (Total TTC within 0.01 tolerance)
            var amountDiff = Math.Abs(candidate.TotalTTC - request.TotalTTC);
            if (amountDiff <= 0.01m)
            {
                score += 0.35m;
                reasons.Add($"Identical TotalTTC: {request.TotalTTC:N4}");
            }
            else if (amountDiff / (request.TotalTTC != 0 ? request.TotalTTC : 1) < 0.005m)
            {
                score += 0.20m;
                reasons.Add($"Near-identical TotalTTC ({candidate.TotalTTC:N4} vs {request.TotalTTC:N4})");
            }

            // 3. Invoice Number Match
            if (!string.IsNullOrEmpty(normalizedReqNumber) && !string.IsNullOrEmpty(candNormNumber))
            {
                if (string.Equals(normalizedReqNumber, candNormNumber, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.35m;
                    reasons.Add($"Identical normalized invoice number: '{normalizedReqNumber}'");
                }
                else
                {
                    var levDistance = ComputeLevenshteinDistance(normalizedReqNumber, candNormNumber);
                    var maxLen = Math.Max(normalizedReqNumber.Length, candNormNumber.Length);
                    if (maxLen > 3 && levDistance == 1)
                    {
                        score += 0.25m;
                        reasons.Add($"Fuzzy invoice number match (distance 1: '{normalizedReqNumber}' vs '{candNormNumber}')");
                    }
                }
            }

            // 4. Date Proximity
            var dateDiffDays = Math.Abs((candidate.InvoiceDate.Date - request.InvoiceDate.Date).TotalDays);
            if (dateDiffDays == 0)
            {
                score += 0.15m;
                reasons.Add("Exact same invoice date");
            }
            else if (dateDiffDays <= 7)
            {
                score += 0.05m;
                reasons.Add($"Invoice date within {dateDiffDays} days");
            }

            // 5. Perceptual Hash (pHash) visual comparison (Hamming distance)
            int hammingDistance = 64;
            if (request.PerceptualHash != 0 && candidate.PerceptualHash != 0)
            {
                hammingDistance = BitOperations.PopCount(request.PerceptualHash ^ candidate.PerceptualHash);
                if (hammingDistance <= 5)
                {
                    score += 0.50m;
                    reasons.Add($"High visual similarity via pHash (Hamming distance: {hammingDistance}/64)");
                }
                else if (hammingDistance <= 10)
                {
                    score += 0.30m;
                    reasons.Add($"Moderate visual similarity via pHash (Hamming distance: {hammingDistance}/64)");
                }
            }

            // Cap score at 1.00
            score = Math.Min(1.00m, score);

            if (score >= 0.70m)
            {
                var isCrossExercice = candidate.ExerciceId != _tenantContext.ExerciceId;
                if (isCrossExercice)
                {
                    reasons.Add($"Cross-exercice duplicate detected: original invoice in fiscal year {candidate.ExerciceId}");
                }

                matches.Add(new DuplicateMatch(
                    candidate.DocumentId,
                    candidate.ExerciceId,
                    isCrossExercice,
                    score,
                    hammingDistance,
                    reasons
                ));
            }
        }

        matches = [.. matches.OrderByDescending(m => m.ConfidenceScore)];
        var bestMatch = matches.FirstOrDefault();
        var isDuplicate = bestMatch != null && bestMatch.ConfidenceScore >= 0.75m;

        return new DuplicateCheckResult(
            isDuplicate,
            bestMatch?.ConfidenceScore ?? 0m,
            bestMatch,
            matches
        );
    }

    public static string NormalizeInvoiceNumber(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return string.Empty;

        // Uppercase, strip non-alphanumeric, strip common invoice prefixes
        var upper = invoiceNumber.ToUpperInvariant().Trim();

        // Remove prefixes
        upper = Regex.Replace(upper, @"^(FACTURE|FACT|FAC|INV|INVOICE|NO|NUM|N°|N)[_/\-\s\.:]*", string.Empty);

        // Strip non-alphanumeric characters
        var alphanumeric = Regex.Replace(upper, @"[^A-Z0-9]", string.Empty);

        // Strip leading zeros
        alphanumeric = alphanumeric.TrimStart('0');

        return alphanumeric;
    }

    public static string NormalizeMatricule(string? matricule)
    {
        if (string.IsNullOrWhiteSpace(matricule))
            return string.Empty;

        return Regex.Replace(matricule.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
    }

    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int j = 1; j <= t.Length; j++)
        {
            for (int i = 1; i <= s.Length; i++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }
}
