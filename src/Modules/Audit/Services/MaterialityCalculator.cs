using ComptaShield.Audit.Entities;

namespace ComptaShield.Audit.Services;

public sealed record MaterialityCalculationResult(
    MaterialityBenchmark Benchmark,
    decimal BenchmarkAmount,
    decimal MaterialityPercentage,
    decimal PlanningMateriality,
    decimal PerformanceMateriality,
    decimal ClearlyTrivialThreshold
);

/// <summary>
/// Computes statutory audit materiality thresholds:
/// 1. Planning Materiality (Seuil de Signification - SSG)
/// 2. Performance Materiality (Erreur Tolérable - ET)
/// 3. Clearly Trivial Threshold (Seuil de remontée des anomalies)
/// based on ISA standards (ISA 320 / ISA 450).
/// </summary>
public sealed class MaterialityCalculator
{
    public MaterialityCalculationResult Calculate(
        MaterialityBenchmark benchmark,
        decimal benchmarkAmount,
        decimal? customPercentage = null,
        decimal performanceMaterialityRatio = 0.75m,
        decimal trivialThresholdRatio = 0.05m)
    {
        if (benchmarkAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(benchmarkAmount), "Benchmark amount must be strictly positive.");

        var percentage = customPercentage ?? GetDefaultBenchmarkPercentage(benchmark);

        var ssg = decimal.Round(benchmarkAmount * percentage, 4, MidpointRounding.ToEven);
        var et = decimal.Round(ssg * performanceMaterialityRatio, 4, MidpointRounding.ToEven);
        var trivial = decimal.Round(ssg * trivialThresholdRatio, 4, MidpointRounding.ToEven);

        return new MaterialityCalculationResult(
            benchmark,
            benchmarkAmount,
            percentage,
            ssg,
            et,
            trivial
        );
    }

    private static decimal GetDefaultBenchmarkPercentage(MaterialityBenchmark benchmark) => benchmark switch
    {
        MaterialityBenchmark.Revenue => 0.01m,      // 1.0% of revenue
        MaterialityBenchmark.TotalAssets => 0.01m,  // 1.0% of total assets
        MaterialityBenchmark.Equity => 0.03m,       // 3.0% of equity
        MaterialityBenchmark.PreTaxProfit => 0.05m, // 5.0% of pre-tax profit
        _ => 0.01m
    };
}
