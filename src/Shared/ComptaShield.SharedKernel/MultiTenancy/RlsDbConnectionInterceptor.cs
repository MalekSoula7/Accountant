using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ComptaShield.SharedKernel.MultiTenancy;

/// <summary>
/// EF Core DbConnection interceptor that sets PostgreSQL session-level configuration
/// variables on every connection open. These variables are used by RLS policies to
/// enforce row-level tenant isolation at the database layer.
///
/// Executes:
///   SELECT set_config('app.current_cabinet_id', '...', true),
///          set_config('app.current_dossier_id', '...', true),
///          set_config('app.current_exercice_id', '...', true);
///
/// The third parameter (true) makes the setting local to the current transaction.
/// </summary>
public sealed class RlsDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public RlsDbConnectionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetRlsVariables(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetRlsVariablesAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetRlsVariables(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BuildSetConfigSql();
        cmd.ExecuteNonQuery();
    }

    private async Task SetRlsVariablesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = BuildSetConfigSql();
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildSetConfigSql()
    {
        return $"""
            SELECT set_config('app.current_cabinet_id', '{_tenantContext.CabinetId}', true),
                   set_config('app.current_dossier_id', '{_tenantContext.DossierId}', true),
                   set_config('app.current_exercice_id', '{_tenantContext.ExerciceId}', true);
            """;
    }
}
