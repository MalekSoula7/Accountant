namespace ComptaShield.FraudShield.Services;

public sealed record DigitDistribution(
    int Digit,
    int ObservedCount,
    decimal ObservedPercentage,
    decimal ExpectedPercentage,
    decimal Deviation
);

public sealed record BenfordTestResult(
    bool AnomalyDetected,
    int SampleSize,
    decimal ChiSquareStatistic,
    decimal CriticalValue95,
    decimal PValue,
    List<DigitDistribution> FirstDigitDistributions,
    List<DigitDistribution> SecondDigitDistributions
);

public sealed record ThresholdSplittingCluster(
    decimal Threshold,
    decimal ClusterRangeMin,
    decimal ClusterRangeMax,
    int TransactionCount,
    decimal TotalAmount,
    List<decimal> SampleAmounts,
    decimal ConcentrationRatio // Proportion of transactions in this band vs expected uniform
);

public sealed record BenfordAndThresholdAuditReport(
    BenfordTestResult BenfordResult,
    List<ThresholdSplittingCluster> ThresholdSplittingClusters,
    bool RequiresAuditorInvestigation,
    decimal RiskScore,
    List<string> AuditFindings
);

/// <summary>
/// Benford's Law forensic analysis engine & threshold splitting detector.
/// Computes 1st and 2nd digit distributions, evaluates Chi-Square goodness-of-fit (df=8),
/// and detects transaction clusters just below corporate approval limits.
/// </summary>
public sealed class BenfordLawAuditService
{
    // Chi-Square critical value for df = 8 at alpha = 0.05
    public const decimal ChiSquareCriticalValue95 = 15.5073m;
    // Chi-Square critical value for df = 8 at alpha = 0.01
    public const decimal ChiSquareCriticalValue99 = 20.0902m;

    // Standard theoretical probabilities for First Digit (Benford's Law)
    private static readonly decimal[] ExpectedFirstDigitProbs =
    [
        0m,          // 0 is invalid for first digit
        0.30103m,    // 1
        0.17609m,    // 2
        0.12494m,    // 3
        0.09691m,    // 4
        0.07918m,    // 5
        0.06695m,    // 6
        0.05799m,    // 7
        0.05115m,    // 8
        0.04576m     // 9
    ];

    // Standard theoretical probabilities for Second Digit (0 to 9)
    private static readonly decimal[] ExpectedSecondDigitProbs =
    [
        0.11968m,    // 0
        0.11389m,    // 1
        0.10882m,    // 2
        0.10433m,    // 3
        0.10031m,    // 4
        0.09668m,    // 5
        0.09337m,    // 6
        0.09035m,    // 7
        0.08757m,    // 8
        0.08500m     // 9
    ];

    /// <summary>
    /// Runs complete forensic audit on a series of positive financial amounts.
    /// </summary>
    public BenfordAndThresholdAuditReport Analyze(
        IReadOnlyList<decimal> amounts,
        IReadOnlyList<decimal>? approvalThresholds = null)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        // Filter valid positive amounts (at least 1.00)
        var validAmounts = amounts.Where(a => a >= 1.00m).ToList();
        var defaultThresholds = approvalThresholds ?? [1000m, 5000m, 10000m, 50000m];

        var findings = new List<string>();
        decimal riskScore = 0m;

        // 1. Benford's Law Test
        var benfordResult = RunBenfordTest(validAmounts);

        if (benfordResult.AnomalyDetected)
        {
            var finding = $"Benford's Law Chi-Square anomaly detected (Chi2 = {benfordResult.ChiSquareStatistic:N2} > {ChiSquareCriticalValue95:N2}, p-value = {benfordResult.PValue:F4}). Numbers deviate significantly from natural distribution.";
            findings.Add(finding);
            riskScore = Math.Max(riskScore, benfordResult.ChiSquareStatistic > ChiSquareCriticalValue99 ? 0.85m : 0.70m);
        }

        // 2. Threshold Splitting / Smurfing Analysis
        var splittingClusters = DetectThresholdSplitting(validAmounts, defaultThresholds);

        foreach (var cluster in splittingClusters)
        {
            var finding = $"Threshold splitting anomaly: {cluster.TransactionCount} transactions concentrated just below approval limit {cluster.Threshold:N2} (band [{cluster.ClusterRangeMin:N2} - {cluster.ClusterRangeMax:N2}]), total volume = {cluster.TotalAmount:N2}.";
            findings.Add(finding);
            riskScore = Math.Max(riskScore, 0.80m);
        }

        var requiresInvestigation = benfordResult.AnomalyDetected || splittingClusters.Count > 0;

        return new BenfordAndThresholdAuditReport(
            benfordResult,
            splittingClusters,
            requiresInvestigation,
            decimal.Round(riskScore, 2, MidpointRounding.ToEven),
            findings
        );
    }

    private static BenfordTestResult RunBenfordTest(List<decimal> amounts)
    {
        if (amounts.Count < 50)
        {
            // Sample too small for statistically valid Chi-Square test (minimum recommended N is 50-100)
            return new BenfordTestResult(
                false, amounts.Count, 0m, ChiSquareCriticalValue95, 1m, [], []);
        }

        var firstDigitCounts = new int[10];
        var secondDigitCounts = new int[10];
        int validCount = 0;

        foreach (var amount in amounts)
        {
            var abs = Math.Abs(amount);
            var str = abs.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                         .TrimStart('0', '.');

            if (str.Length >= 1 && char.IsDigit(str[0]))
            {
                var d1 = str[0] - '0';
                if (d1 >= 1 && d1 <= 9)
                {
                    firstDigitCounts[d1]++;
                    validCount++;

                    if (str.Length >= 2 && char.IsDigit(str[1]))
                    {
                        var d2 = str[1] - '0';
                        if (d2 >= 0 && d2 <= 9)
                        {
                            secondDigitCounts[d2]++;
                        }
                    }
                }
            }
        }

        if (validCount == 0)
        {
            return new BenfordTestResult(false, 0, 0m, ChiSquareCriticalValue95, 1m, [], []);
        }

        // Calculate Chi-Square for First Digit
        decimal chiSquare = 0m;
        var firstDigitDists = new List<DigitDistribution>();

        for (int d = 1; d <= 9; d++)
        {
            var observed = firstDigitCounts[d];
            var observedPct = (decimal)observed / validCount;
            var expectedPct = ExpectedFirstDigitProbs[d];
            var expectedCount = validCount * expectedPct;

            var deviation = observedPct - expectedPct;
            var diff = (decimal)observed - expectedCount;
            chiSquare += (diff * diff) / (expectedCount != 0 ? expectedCount : 1m);

            firstDigitDists.Add(new DigitDistribution(
                d, observed, observedPct, expectedPct, deviation));
        }

        // Calculate distributions for Second Digit
        var secondDigitDists = new List<DigitDistribution>();
        for (int d = 0; d <= 9; d++)
        {
            var observed = secondDigitCounts[d];
            var observedPct = validCount > 0 ? (decimal)observed / validCount : 0m;
            var expectedPct = ExpectedSecondDigitProbs[d];
            secondDigitDists.Add(new DigitDistribution(
                d, observed, observedPct, expectedPct, observedPct - expectedPct));
        }

        // Approximate p-value from ChiSquare with df=8 using Wilson-Hilferty transformation
        var pValue = ApproximateChiSquarePValue((double)chiSquare, 8);
        var isAnomaly = chiSquare > ChiSquareCriticalValue95;

        return new BenfordTestResult(
            isAnomaly,
            validCount,
            decimal.Round(chiSquare, 4, MidpointRounding.ToEven),
            ChiSquareCriticalValue95,
            decimal.Round((decimal)pValue, 4, MidpointRounding.ToEven),
            firstDigitDists,
            secondDigitDists
        );
    }

    private static List<ThresholdSplittingCluster> DetectThresholdSplitting(
        List<decimal> amounts,
        IReadOnlyList<decimal> thresholds)
    {
        var clusters = new List<ThresholdSplittingCluster>();

        foreach (var threshold in thresholds)
        {
            // Focus on amounts in the band [0.85 * threshold, 0.999 * threshold]
            var bandMin = decimal.Round(threshold * 0.85m, 2);
            var bandMax = decimal.Round(threshold * 0.999m, 2);

            var inBand = amounts.Where(a => a >= bandMin && a <= bandMax).ToList();

            // Also check how many are in the wider neighborhood [0.70 * threshold, 1.15 * threshold]
            var neighborhoodMin = threshold * 0.70m;
            var neighborhoodMax = threshold * 1.15m;
            var inNeighborhood = amounts.Count(a => a >= neighborhoodMin && a <= neighborhoodMax);

            if (inBand.Count >= 3 && inNeighborhood > 0)
            {
                // Expected share of the sub-band within the neighborhood assuming uniform distribution
                var bandWidth = bandMax - bandMin;
                var neighborhoodWidth = neighborhoodMax - neighborhoodMin;
                var expectedRatio = bandWidth / neighborhoodWidth;

                var actualRatio = (decimal)inBand.Count / inNeighborhood;

                // If concentration in the sub-band is more than 2.0x expected, flag it as threshold splitting
                if (actualRatio >= expectedRatio * 2.0m || inBand.Count >= 5)
                {
                    clusters.Add(new ThresholdSplittingCluster(
                        threshold,
                        bandMin,
                        bandMax,
                        inBand.Count,
                        inBand.Sum(),
                        inBand.Take(5).ToList(),
                        decimal.Round(actualRatio / (expectedRatio > 0 ? expectedRatio : 1m), 2, MidpointRounding.ToEven)
                    ));
                }
            }
        }

        return clusters;
    }

    /// <summary>
    /// Approximates the p-value P(X >= chi2) for df degrees of freedom using the regularized gamma function.
    /// </summary>
    private static double ApproximateChiSquarePValue(double x, int df)
    {
        if (x <= 0) return 1.0;
        if (df <= 0) return 0.0;

        // P(X >= x) = 1 - P(X < x) = GammaQ(df/2, x/2)
        var k = df / 2.0;
        var z = x / 2.0;

        return IncompleteGammaQ(k, z);
    }

    private static double IncompleteGammaQ(double a, double x)
    {
        // Continued fraction approximation for upper incomplete gamma Q(a, x) = 1 - P(a, x)
        if (x < a + 1.0)
        {
            return 1.0 - IncompleteGammaP(a, x);
        }

        double b = x + 1.0 - a;
        double c = 1.0 / 1e-30;
        double d = 1.0 / b;
        double h = d;

        for (int i = 1; i <= 100; i++)
        {
            double an = -i * (i - a);
            b += 2.0;
            d = an * d + b;
            if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = b + an / c;
            if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1.0 / d;
            double del = d * c;
            h *= del;
            if (Math.Abs(del - 1.0) < 1e-10) break;
        }

        return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    private static double IncompleteGammaP(double a, double x)
    {
        // Series expansion for lower incomplete gamma P(a, x)
        double sum = 1.0 / a;
        double term = sum;

        for (int n = 1; n <= 100; n++)
        {
            term *= x / (a + n);
            sum += term;
            if (Math.Abs(term) < Math.Abs(sum) * 1e-10) break;
        }

        return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * sum;
    }

    private static double LogGamma(double x)
    {
        // Lanczos approximation (g=5, n=6)
        double[] c =
        [
            76.18009172947146,
            -86.50532032941677,
            24.01409824083091,
            -1.231739572450155,
            0.1208650973866179e-2,
            -0.5395239384953e-5
        ];

        double y = x;
        double tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;

        for (int j = 0; j <= 5; j++)
        {
            ser += c[j] / ++y;
        }

        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }
}
