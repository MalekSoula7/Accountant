using System.Text.Json;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ComptaShield.Tenancy.Services;

/// <summary>
/// Redis-first permission checker with PostgreSQL fallback.
/// Cache key pattern: cabinet:{cabinetId}:user:{userId}:dossiers
/// Value: JSON dictionary { dossierId: role }
/// TTL: 15 minutes (refreshed on access).
/// </summary>
public sealed class RedisPermissionChecker : IPermissionChecker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TenancyDbContext _dbContext;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public RedisPermissionChecker(IConnectionMultiplexer redis, TenancyDbContext dbContext)
    {
        _redis = redis;
        _dbContext = dbContext;
    }

    public async Task<DossierRole?> GetUserRoleAsync(Guid cabinetId, Guid userId, Guid dossierId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = BuildCacheKey(cabinetId, userId);

        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(cached!);
            if (map != null && map.TryGetValue(dossierId.ToString(), out var roleStr) &&
                Enum.TryParse<DossierRole>(roleStr, out var cachedRole))
            {
                await db.KeyExpireAsync(cacheKey, CacheTtl);
                return cachedRole;
            }

            // Key exists but dossier not in it — user has no access
            if (map != null)
                return null;
        }

        // Cache miss: load all assignments for this user within this cabinet
        var assignments = await _dbContext.DossierAssignments
            .Where(a => a.CabinetId == cabinetId && a.UserId == userId)
            .ToDictionaryAsync(a => a.DossierId.ToString(), a => a.Role.ToString(), ct);

        var json = JsonSerializer.Serialize(assignments);
        await db.StringSetAsync(cacheKey, json, CacheTtl);

        return assignments.TryGetValue(dossierId.ToString(), out var roleVal) &&
               Enum.TryParse<DossierRole>(roleVal, out var role)
            ? role
            : null;
    }

    public async Task InvalidateCacheAsync(Guid cabinetId, Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(BuildCacheKey(cabinetId, userId));
    }

    private static string BuildCacheKey(Guid cabinetId, Guid userId)
        => $"cabinet:{cabinetId}:user:{userId}:dossiers";
}
