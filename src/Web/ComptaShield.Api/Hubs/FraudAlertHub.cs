using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ComptaShield.Api.Hubs;

/// <summary>
/// Real-time SignalR hub streaming live fraud alerts to the frontend SOC dashboard.
/// Connected clients are automatically grouped by Cabinet and Dossier.
/// </summary>
[Authorize]
public sealed class FraudAlertHub : Hub
{
    private readonly ITenantContext _tenantContext;

    public FraudAlertHub(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task OnConnectedAsync()
    {
        // Automatically join the cabinet-wide and dossier-scoped real-time groups
        var cabinetGroup = $"cabinet:{_tenantContext.CabinetId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, cabinetGroup);

        if (_tenantContext.DossierId != Guid.Empty)
        {
            var dossierGroup = $"cabinet:{_tenantContext.CabinetId}:dossier:{_tenantContext.DossierId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, dossierGroup);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinDossierGroup(string dossierId)
    {
        if (Guid.TryParse(dossierId, out var parsedDossierId))
        {
            var group = $"cabinet:{_tenantContext.CabinetId}:dossier:{parsedDossierId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }
    }

    public async Task LeaveDossierGroup(string dossierId)
    {
        if (Guid.TryParse(dossierId, out var parsedDossierId))
        {
            var group = $"cabinet:{_tenantContext.CabinetId}:dossier:{parsedDossierId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }
    }
}
