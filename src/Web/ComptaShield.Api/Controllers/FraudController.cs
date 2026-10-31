using ComptaShield.Api.Hubs;
using ComptaShield.FraudShield.Entities;
using ComptaShield.FraudShield.Persistence;
using ComptaShield.FraudShield.Services;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Api.Controllers;

public sealed record QuarantineRequest(string Reason);
public sealed record TicketRequest(string QuestionPrompt, int ValidityDays = 7);
public sealed record ReversingEntryRequest(Guid OriginalEntryId, string OriginalEntryNumber, List<JournalLineDto> Lines, string Reason);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class FraudController : ControllerBase
{
    private readonly FraudDbContext _dbContext;
    private readonly FraudRectificationService _rectificationService;
    private readonly BenfordLawAuditService _benfordService;
    private readonly IHubContext<FraudAlertHub> _hubContext;
    private readonly ITenantContext _tenantContext;

    public FraudController(
        FraudDbContext dbContext,
        FraudRectificationService rectificationService,
        BenfordLawAuditService benfordService,
        IHubContext<FraudAlertHub> hubContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _rectificationService = rectificationService;
        _benfordService = benfordService;
        _hubContext = hubContext;
        _tenantContext = tenantContext;
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] FraudAlertStatus? status, CancellationToken ct)
    {
        var query = _dbContext.Alerts.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var alerts = await query.OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync(ct);
        return Ok(alerts);
    }

    [HttpPost("alerts/{id:guid}/quarantine")]
    public async Task<IActionResult> QuarantineAlert(Guid id, [FromBody] QuarantineRequest request, CancellationToken ct)
    {
        var success = await _rectificationService.QuarantineDocumentAsync(id, request.Reason, ct);
        if (!success) return NotFound();

        // Broadcast real-time update to SOC clients via SignalR
        await _hubContext.Clients
            .Group($"cabinet:{_tenantContext.CabinetId}:dossier:{_tenantContext.DossierId}")
            .SendAsync("AlertStatusChanged", new { AlertId = id, Status = FraudAlertStatus.Quarantined.ToString(), Reason = request.Reason }, ct);

        return Ok(new { success = true });
    }

    [HttpPost("alerts/{id:guid}/tickets")]
    public async Task<IActionResult> CreateTicket(Guid id, [FromBody] TicketRequest request, CancellationToken ct)
    {
        var ticket = await _rectificationService.GenerateClientClarificationTicketAsync(
            id, request.QuestionPrompt, TimeSpan.FromDays(request.ValidityDays), ct);

        return Ok(ticket);
    }

    [HttpPost("reversing-proposal")]
    public IActionResult CreateReversingProposal([FromBody] ReversingEntryRequest request)
    {
        var proposal = _rectificationService.CreateReversingEntryProposal(
            request.OriginalEntryId, request.OriginalEntryNumber, request.Lines, request.Reason);

        return Ok(proposal);
    }

    [HttpPost("benford-analysis")]
    public IActionResult AnalyzeBenford([FromBody] List<decimal> amounts)
    {
        var report = _benfordService.Analyze(amounts);
        return Ok(report);
    }
}
