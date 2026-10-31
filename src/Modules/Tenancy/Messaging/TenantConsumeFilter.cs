using System.Text.Json;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using MassTransit;

namespace ComptaShield.Tenancy.Messaging;

/// <summary>
/// MassTransit consume filter that extracts TenantMessageHeader from incoming messages
/// and hydrates the scoped ITenantContext so downstream consumers and EF Core contexts
/// operate with the correct tenant scope.
/// </summary>
public sealed class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ScopedTenantContext _tenantContext;

    public TenantConsumeFilter(ITenantContext tenantContext)
    {
        _tenantContext = (ScopedTenantContext)tenantContext;
    }

    public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (context.Headers.TryGetHeader(TenantMessageHeader.HeaderKey, out var headerObj) &&
            headerObj is string headerJson)
        {
            var header = JsonSerializer.Deserialize<TenantMessageHeader>(headerJson);
            if (header is not null)
            {
                _tenantContext.CabinetId = header.CabinetId;
                _tenantContext.DossierId = header.DossierId;
                _tenantContext.ExerciceId = header.ExerciceId;
                _tenantContext.UserId = header.InitiatedByUserId;

                if (Enum.TryParse<DossierRole>(header.Role, out var role))
                    _tenantContext.Role = role;
            }
        }

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-consume");
}
