using ComptaShield.Audit.Entities;
using ComptaShield.Audit.Persistence;
using ComptaShield.Audit.Services;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Api.Controllers;

public sealed record MaterialityRequest(
    MaterialityBenchmark Benchmark,
    decimal BenchmarkAmount,
    decimal? CustomPercentage = null
);

public sealed record SamplingRequest(
    List<PopulationItem> Population,
    decimal PerformanceMateriality,
    decimal ReliabilityFactor = 3.0m
);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AuditController : ControllerBase
{
    private readonly AuditDbContext _dbContext;
    private readonly MaterialityCalculator _materialityCalculator;
    private readonly MonetaryUnitSamplingSelector _samplingSelector;
    private readonly ITenantContext _tenantContext;

    public AuditController(
        AuditDbContext dbContext,
        MaterialityCalculator materialityCalculator,
        MonetaryUnitSamplingSelector samplingSelector,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _materialityCalculator = materialityCalculator;
        _samplingSelector = samplingSelector;
        _tenantContext = tenantContext;
    }

    [HttpGet("engagements")]
    public async Task<IActionResult> GetEngagements(CancellationToken ct)
    {
        var engagements = await _dbContext.Engagements
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
        return Ok(engagements);
    }

    [HttpPost("calculate-materiality")]
    public IActionResult CalculateMateriality([FromBody] MaterialityRequest request)
    {
        var result = _materialityCalculator.Calculate(
            request.Benchmark, request.BenchmarkAmount, request.CustomPercentage);
        return Ok(result);
    }

    [HttpPost("mus-sample")]
    public IActionResult SelectMusSample([FromBody] SamplingRequest request)
    {
        var result = _samplingSelector.SelectMusSample(
            request.Population, request.PerformanceMateriality, request.ReliabilityFactor);
        return Ok(result);
    }
}
