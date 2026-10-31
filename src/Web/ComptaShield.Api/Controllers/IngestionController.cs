using ComptaShield.Ingestion.Entities;
using ComptaShield.Ingestion.Persistence;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class IngestionController : ControllerBase
{
    private readonly IngestionDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public IngestionController(IngestionDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] DocumentStatus? status, CancellationToken ct)
    {
        var query = _dbContext.Documents.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var docs = await query
            .OrderByDescending(d => d.UploadedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(docs);
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<IActionResult> GetDocumentById(Guid id, CancellationToken ct)
    {
        var doc = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();
        return Ok(doc);
    }
}
