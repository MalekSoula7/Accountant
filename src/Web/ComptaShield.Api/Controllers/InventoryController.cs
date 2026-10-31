using ComptaShield.Inventory.Persistence;
using ComptaShield.Inventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Api.Controllers;

public sealed record PhysicalReconciliationRequest(
    string CountReference,
    DateTime CountDate,
    List<PhysicalCountItemInput> Counts
);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly InventoryReconciliationService _reconciliationService;

    public InventoryController(
        InventoryDbContext dbContext,
        InventoryReconciliationService reconciliationService)
    {
        _dbContext = dbContext;
        _reconciliationService = reconciliationService;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetStockItems(CancellationToken ct)
    {
        var items = await _dbContext.StockItems.OrderBy(s => s.ItemCode).ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("reconcile-count")]
    public async Task<IActionResult> ReconcileCount([FromBody] PhysicalReconciliationRequest request, CancellationToken ct)
    {
        var report = await _reconciliationService.ReconcileAndValidateCountAsync(
            request.CountReference, request.CountDate, request.Counts, ct);

        return Ok(report);
    }
}
