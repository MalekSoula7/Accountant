using ComptaShield.LedgerAndBilan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComptaShield.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class LedgerController : ControllerBase
{
    private readonly BilanCalculationService _bilanService;

    public LedgerController(BilanCalculationService bilanService)
    {
        _bilanService = bilanService;
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance(CancellationToken ct)
    {
        var result = await _bilanService.GenerateTrialBalanceAsync(ct);
        return Ok(result);
    }

    [HttpGet("bilan-actif")]
    public async Task<IActionResult> GetBilanActif(CancellationToken ct)
    {
        var result = await _bilanService.GenerateBilanActifAsync(ct);
        return Ok(result);
    }

    [HttpGet("bilan-passif")]
    public async Task<IActionResult> GetBilanPassif(CancellationToken ct)
    {
        var result = await _bilanService.GenerateBilanPassifAsync(ct);
        return Ok(result);
    }

    [HttpGet("etat-de-resultat")]
    public async Task<IActionResult> GetEtatDeResultat(CancellationToken ct)
    {
        var result = await _bilanService.GenerateEtatDeResultatAsync(ct);
        return Ok(result);
    }
}
