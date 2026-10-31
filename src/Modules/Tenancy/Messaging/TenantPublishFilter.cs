using System.Text.Json;
using ComptaShield.SharedKernel.MultiTenancy;
using MassTransit;

namespace ComptaShield.Tenancy.Messaging;

/// <summary>
/// MassTransit publish filter that attaches TenantMessageHeader to every outgoing message.
/// </summary>
public sealed class TenantPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ITenantContext _tenantContext;

    public TenantPublishFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var header = new TenantMessageHeader
        {
            CabinetId = _tenantContext.CabinetId,
            DossierId = _tenantContext.DossierId,
            ExerciceId = _tenantContext.ExerciceId,
            InitiatedByUserId = _tenantContext.UserId,
            Role = _tenantContext.Role.ToString()
        };

        context.Headers.Set(TenantMessageHeader.HeaderKey, JsonSerializer.Serialize(header));

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-publish");
}
