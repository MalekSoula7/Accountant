using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace ComptaShield.FraudShield.Services;

public sealed record PdfForensicReport(
    bool IsSuspicious,
    decimal RiskScore,
    string? Producer,
    string? Creator,
    double? ModificationGapHours,
    List<string> DetectedSoftwareAnomalies,
    List<string> FontAnomalies,
    List<string> Findings
);

/// <summary>
/// Scans inbound PDF invoices for tampering indicators:
/// 1. Graphic/online PDF editor signatures (Photoshop, Canva, GIMP, iLovePDF, Sejda, etc.)
/// 2. Discrepancies between CreationDate and ModDate indicating post-generation alteration
/// 3. Font subset anomalies where numeric/total areas use fonts differing from document body
/// </summary>
public sealed class PdfMetadataForensicsScanner
{
    private static readonly string[] SuspiciousSoftwareKeywords =
    [
        "photoshop", "canva", "gimp", "ilovepdf", "sejda", "pdfescape",
        "smallpdf", "illustrator", "inkscape", "coreldraw", "paint.net"
    ];

    public PdfForensicReport Scan(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        var softwareAnomalies = new List<string>();
        var fontAnomalies = new List<string>();
        var findings = new List<string>();

        string? producer = null;
        string? creator = null;
        double? modGapHours = null;
        decimal riskScore = 0m;

        try
        {
            using var document = PdfDocument.Open(pdfStream);
            var info = document.Information;

            producer = info.Producer;
            creator = info.Creator;

            // 1. Inspect Producer and Creator
            var combinedInfo = $"{producer} {creator}".ToLowerInvariant();

            foreach (var keyword in SuspiciousSoftwareKeywords)
            {
                if (combinedInfo.Contains(keyword))
                {
                    var anomaly = $"Suspicious editing software signature found in metadata: '{keyword}' (Producer: '{producer}', Creator: '{creator}')";
                    softwareAnomalies.Add(anomaly);
                    findings.Add(anomaly);
                    riskScore = Math.Max(riskScore, 0.85m);
                }
            }

            // 2. Creation vs Modification Timestamp Gap
            DateTime? creationDate = ParsePdfDate(info.CreationDate);
            DateTime? modDate = ParsePdfDate(info.ModifiedDate);

            if (creationDate.HasValue && modDate.HasValue)
            {
                var gap = (modDate.Value - creationDate.Value).TotalHours;
                modGapHours = gap;

                // If document was modified more than 24 hours after creation, or if modified earlier than created
                if (gap < -0.1) // modified before created -> clock manipulation
                {
                    var finding = $"Anomalous metadata timestamps: Modification date ({modDate}) precedes Creation date ({creationDate}).";
                    findings.Add(finding);
                    riskScore = Math.Max(riskScore, 0.90m);
                }
                else if (gap > 48.0 && softwareAnomalies.Count > 0)
                {
                    var finding = $"Document modified {gap:F1} hours after initial generation with an external editing tool.";
                    findings.Add(finding);
                    riskScore = Math.Max(riskScore, 0.95m);
                }
            }

            // 3. Font Distribution & Subset Analysis
            var fontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var numericFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var alphaFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var page in document.GetPages())
            {
                foreach (var letter in page.Letters)
                {
                    if (letter.FontName != null)
                    {
                        fontNames.Add(letter.FontName);

                        if (char.IsDigit(letter.Value[0]))
                        {
                            numericFonts.Add(letter.FontName);
                        }
                        else if (char.IsLetter(letter.Value[0]))
                        {
                            alphaFonts.Add(letter.FontName);
                        }
                    }
                }
            }

            // Check if there are numeric characters printed using a font that appears nowhere in alphabetical text
            var isolatedNumericFonts = numericFonts.Except(alphaFonts).ToList();
            if (isolatedNumericFonts.Count > 0 && alphaFonts.Count > 0)
            {
                var anomaly = $"Isolated font subset anomaly: Font(s) '{string.Join(", ", isolatedNumericFonts)}' used exclusively for numbers/totals and absent from text.";
                fontAnomalies.Add(anomaly);
                findings.Add(anomaly);
                riskScore = Math.Max(riskScore, 0.75m);
            }
        }
        catch (Exception ex)
        {
            findings.Add($"Forensic scan failed or PDF stream unreadable: {ex.Message}");
            riskScore = Math.Max(riskScore, 0.50m);
        }

        var isSuspicious = riskScore >= 0.70m;

        return new PdfForensicReport(
            isSuspicious,
            decimal.Round(riskScore, 2, MidpointRounding.ToEven),
            producer,
            creator,
            modGapHours,
            softwareAnomalies,
            fontAnomalies,
            findings
        );
    }

    /// <summary>
    /// Parses standard PDF dates (D:YYYYMMDDHHmmSSOHH'mm') or ISO dates.
    /// </summary>
    private static DateTime? ParsePdfDate(string? pdfDate)
    {
        if (string.IsNullOrWhiteSpace(pdfDate))
            return null;

        var clean = pdfDate.Trim();
        if (clean.StartsWith("D:", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..];

        var match = Regex.Match(clean, @"^(\d{4})(\d{2})(\d{2})(\d{2})?(\d{2})?(\d{2})?");
        if (match.Success)
        {
            var year = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var day = int.Parse(match.Groups[3].Value);
            var hour = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            var minute = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
            var second = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 0;

            try
            {
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            }
            catch
            {
                return null;
            }
        }

        if (DateTime.TryParse(pdfDate, out var dt))
            return dt.ToUniversalTime();

        return null;
    }
}
