using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Tenancy.IntegrationTests;

/// <summary>
/// Tests verifying that ScopedModuleDbContext enforces tenant isolation via
/// Global Query Filters, auto-stamping on Add, mutation blocking, and locked Exercice rejection.
/// </summary>
public sealed class TenantIsolationTests
{
    private static readonly Guid CabinetA = Guid.NewGuid();
    private static readonly Guid CabinetB = Guid.NewGuid();
    private static readonly Guid DossierA = Guid.NewGuid();
    private static readonly Guid DossierB = Guid.NewGuid();
    private static readonly Guid ExerciceA = Guid.NewGuid();
    private static readonly Guid ExerciceB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static (TestScopedDbContext db, ScopedTenantContext tenant) CreateContext(
        string dbName,
        Guid cabinetId, Guid dossierId, Guid exerciceId,
        bool isLocked = false)
    {
        var tenant = new ScopedTenantContext
        {
            UserId = UserId,
            CabinetId = cabinetId,
            DossierId = dossierId,
            ExerciceId = exerciceId,
            Role = DossierRole.Partner,
            IsExerciceLocked = isLocked
        };

        var options = new DbContextOptionsBuilder<TestScopedDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new TestScopedDbContext(options, tenant), tenant);
    }

    [Fact]
    public async Task Query_DossierA_Should_Not_Return_DossierB_Rows()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed data as Dossier A context
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);
            db.Documents.Add(new TestDocument { Id = Guid.NewGuid(), Title = "DocA" });
            await db.SaveChangesAsync();
            await db.DisposeAsync();
        }

        // Seed data as Dossier B context (same DB, different tenant)
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierB, ExerciceB);
            db.Documents.Add(new TestDocument { Id = Guid.NewGuid(), Title = "DocB" });
            await db.SaveChangesAsync();
            await db.DisposeAsync();
        }

        // Query as Dossier A — should only see DocA
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);
            var docs = await db.Documents.ToListAsync();
            Assert.Single(docs);
            Assert.Equal("DocA", docs[0].Title);
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Query_CabinetB_Should_Not_Return_CabinetA_Rows()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed as Cabinet A
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);
            db.Documents.Add(new TestDocument { Id = Guid.NewGuid(), Title = "CabA-Doc" });
            await db.SaveChangesAsync();
            await db.DisposeAsync();
        }

        // Query as Cabinet B
        {
            var (db, _) = CreateContext(dbName, CabinetB, DossierA, ExerciceA);
            var docs = await db.Documents.ToListAsync();
            Assert.Empty(docs);
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task SaveChanges_Should_AutoStamp_TenantKeys_On_Added()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);

        var doc = new TestDocument { Id = Guid.NewGuid(), Title = "Test" };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        // The entity should have been stamped with the tenant context values
        Assert.Equal(CabinetA, doc.CabinetId);
        Assert.Equal(DossierA, doc.DossierId);
        Assert.Equal(ExerciceA, doc.ExerciceId);

        await db.DisposeAsync();
    }

    [Fact]
    public async Task SaveChanges_Should_Reject_Writes_When_Exercice_Locked()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA, isLocked: true);

        db.Documents.Add(new TestDocument { Id = Guid.NewGuid(), Title = "Blocked" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);

        await db.DisposeAsync();
    }

    [Fact]
    public async Task SaveChanges_Should_Block_TenantKey_Mutation_On_Update()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed
        var docId = Guid.NewGuid();
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);
            db.Documents.Add(new TestDocument { Id = docId, Title = "Original" });
            await db.SaveChangesAsync();
            await db.DisposeAsync();
        }

        // Attempt to mutate CabinetId
        {
            var (db, _) = CreateContext(dbName, CabinetA, DossierA, ExerciceA);
            var doc = await db.Documents.FindAsync(docId);
            Assert.NotNull(doc);
            doc!.CabinetId = Guid.NewGuid(); // Forbidden mutation
            doc.Title = "Modified";

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("tenant keys", ex.Message, StringComparison.OrdinalIgnoreCase);

            await db.DisposeAsync();
        }
    }
}
