using System.Security.Claims;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using ComptaShield.Tenancy.Persistence;
using ComptaShield.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Api.Middleware;

/// <summary>
/// ASP.NET Core middleware that resolves the tenant context for every request.
/// - Extracts UserId and CabinetId from the JWT claims.
/// - Reads X-Dossier-Id and X-Exercice-Id from request headers.
/// - Verifies access via IPermissionChecker (Redis-cached).
/// - Populates the scoped ScopedTenantContext for downstream use.
/// - Returns 403 Forbidden if the user has no assignment to the requested Dossier.
/// </summary>
public sealed class TenantScopeMiddleware
{
    private readonly RequestDelegate _next;

    public TenantScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ScopedTenantContext tenantContext,
        IPermissionChecker permissionChecker,
        TenancyDbContext tenancyDb)
    {
        // Skip tenant resolution for unauthenticated requests (login, health, etc.)
        if (httpContext.User.Identity is not { IsAuthenticated: true })
        {
            await _next(httpContext);
            return;
        }

        // Extract UserId from JWT 'sub' claim
        var subClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? httpContext.User.FindFirstValue("sub");
        if (!Guid.TryParse(subClaim, out var userId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Extract CabinetId from JWT 'cabinet_id' claim
        var cabinetClaim = httpContext.User.FindFirstValue("cabinet_id");
        if (!Guid.TryParse(cabinetClaim, out var cabinetId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        tenantContext.UserId = userId;
        tenantContext.CabinetId = cabinetId;

        // Read optional Dossier/Exercice headers
        Guid dossierId = default;
        Guid exerciceId = default;

        var hasDossier = httpContext.Request.Headers.TryGetValue("X-Dossier-Id", out var dossierHeader)
                         && Guid.TryParse(dossierHeader, out dossierId);

        var hasExercice = httpContext.Request.Headers.TryGetValue("X-Exercice-Id", out var exerciceHeader)
                          && Guid.TryParse(exerciceHeader, out exerciceId);

        if (hasDossier)
        {
            // Verify dossier access
            var role = await permissionChecker.GetUserRoleAsync(cabinetId, userId, dossierId);
            if (role is null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsync("Access denied: no assignment to the requested Dossier.");
                return;
            }

            tenantContext.DossierId = dossierId;
            tenantContext.Role = role.Value;

            if (hasExercice)
            {
                // Verify exercice belongs to this dossier and check lock status
                var exercice = await tenancyDb.Exercices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.CabinetId == cabinetId
                                              && e.DossierId == dossierId
                                              && e.Id == exerciceId);

                if (exercice is null)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await httpContext.Response.WriteAsync("Access denied: Exercice not found in the requested Dossier.");
                    return;
                }

                tenantContext.ExerciceId = exerciceId;
                tenantContext.IsExerciceLocked = exercice.IsLocked;
            }
        }

        await _next(httpContext);
    }
}
