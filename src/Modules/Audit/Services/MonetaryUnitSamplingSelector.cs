namespace ComptaShield.Audit.Services;

public sealed record PopulationItem(
    string Identifier,
    string Description,
    decimal BookValue
);

public sealed record SampledAuditItem(
    string Identifier,
    string Description,
    decimal BookValue,
    string Stratum,
    bool IsTopStratumKeyItem,
    int HitCount
);

public sealed record MonetaryUnitSamplingResult(
    decimal TotalPopulationValue,
    int TotalPopulationCount,
    decimal SamplingInterval,
    int SampleSize,
    List<SampledAuditItem> TopStratumItems,
    List<SampledAuditItem> SampledItems,
    decimal CoveragePercentage
);

/// <summary>
/// Implements Monetary Unit Sampling (MUS) and stratified statistical sampling for audit workpapers.
/// High-value items >= sampling interval receive 100% testing (Top Stratum).
/// Remainder sampled systematically with probability proportional to monetary size.
/// </summary>
public sealed class MonetaryUnitSamplingSelector
{
    /// <summary>
    /// Executes Monetary Unit Sampling on a population.
    /// Sampling Interval = Tolerable Error / Reliability Factor (standard 3.0 for 95% confidence).
    /// </summary>
    public MonetaryUnitSamplingResult SelectMusSample(
        IReadOnlyList<PopulationItem> population,
        decimal performanceMateriality,
        decimal reliabilityFactor = 3.0m,
        decimal? randomStartSeed = null)
    {
        ArgumentNullException.ThrowIfNull(population);
        if (population.Count == 0)
            throw new ArgumentException("Population cannot be empty.", nameof(population));
        if (performanceMateriality <= 0m)
            throw new ArgumentOutOfRangeException(nameof(performanceMateriality), "Performance materiality must be positive.");

        var totalPopulationValue = decimal.Round(population.Sum(p => p.BookValue), 4, MidpointRounding.ToEven);
        var samplingInterval = decimal.Round(performanceMateriality / reliabilityFactor, 4, MidpointRounding.ToEven);

        if (samplingInterval <= 0m) samplingInterval = 1m;

        var topStratum = new List<SampledAuditItem>();
        var sampleCandidatePopulation = new List<PopulationItem>();

        // 1. Separate Top Stratum (individually significant items >= sampling interval)
        foreach (var item in population)
        {
            if (item.BookValue >= samplingInterval)
            {
                topStratum.Add(new SampledAuditItem(
                    item.Identifier,
                    item.Description,
                    item.BookValue,
                    "TopStratum",
                    true,
                    1
                ));
            }
            else if (item.BookValue > 0m)
            {
                sampleCandidatePopulation.Add(item);
            }
        }

        // 2. Systematic Monetary Unit Sampling on remaining items
        var sampled = new List<SampledAuditItem>();
        var randomStart = randomStartSeed ?? (samplingInterval * 0.5m);
        var currentMilestone = randomStart;
        decimal cumulativeValue = 0m;

        var itemHitCounts = new Dictionary<string, int>();

        foreach (var item in sampleCandidatePopulation)
        {
            cumulativeValue += item.BookValue;

            int hits = 0;
            while (cumulativeValue >= currentMilestone)
            {
                hits++;
                currentMilestone += samplingInterval;
            }

            if (hits > 0)
            {
                itemHitCounts[item.Identifier] = itemHitCounts.GetValueOrDefault(item.Identifier) + hits;
            }
        }

        foreach (var item in sampleCandidatePopulation)
        {
            if (itemHitCounts.TryGetValue(item.Identifier, out var hits))
            {
                sampled.Add(new SampledAuditItem(
                    item.Identifier,
                    item.Description,
                    item.BookValue,
                    "RepresentativeSample",
                    false,
                    hits
                ));
            }
        }

        var totalSampledValue = topStratum.Sum(i => i.BookValue) + sampled.Sum(i => i.BookValue);
        var coveragePct = totalPopulationValue > 0m
            ? decimal.Round((totalSampledValue / totalPopulationValue) * 100m, 2, MidpointRounding.ToEven)
            : 0m;

        return new MonetaryUnitSamplingResult(
            totalPopulationValue,
            population.Count,
            samplingInterval,
            topStratum.Count + sampled.Count,
            topStratum,
            sampled,
            coveragePct
        );
    }
}
