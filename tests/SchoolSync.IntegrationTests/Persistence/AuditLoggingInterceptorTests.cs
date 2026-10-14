using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Persistence;

// CT-003 gate: "Adversarial test proves no bypass path." Per 07-business-logic/_shared.md §3's
// binding requirement (company.md §7): "at least one test must attempt to mutate a governed
// entity through a path that bypasses the interceptor ... and demonstrate that path is not
// exposed in production code — the suite may not merely assert 'a normal save produces an audit
// row.'"
public class AuditLoggingInterceptorTests
{
    private sealed class FakeAuditContext(Guid? actorUserId) : IAuditContext
    {
        public Guid? ActorUserId { get; } = actorUserId;
        public string CorrelationId { get; } = Guid.NewGuid().ToString();
    }

    private static (SchoolSyncDbContext context, Guid institutionId, Guid teacherId) BuildSeededContext(
        string dbName, Guid actorUserId)
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new AuditLoggingInterceptor(new FakeAuditContext(actorUserId)))
            .Options;

        var context = new SchoolSyncDbContext(options, new FakeCurrentTenant { BypassTenantFilter = true });

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Entry",
            LearnerCapacity = 200,
            StorageAllowanceGb = 5,
            SupportLevel = "Standard",
            CapabilitiesJson = "[]"
        };
        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = "Audit Test School",
            RegistrationNumber = "REG-AUDIT",
            ContactEmail = "audit@example.edu",
            Telephone = "+91 90000 01111",
            PostalAddress = "1 Audit St",
            PostalCode = "000002",
            City = "Testville",
            Region = "Test Region",
            SubscriptionPlanId = plan.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            Name = "Dr. Sarah Smith",
            ContactEmail = "sarah@audit.edu",
            Telephone = "+91 90000 02222",
            Gender = "Female",
            Address = "2 Audit St",
            JoiningDate = new DateOnly(2019, 6, 10)
        };
        context.SubscriptionPlans.Add(plan);
        context.Institutions.Add(institution);
        context.Teachers.Add(teacher);
        context.SaveChanges(); // this save itself produces an AuditLog row for the Teacher (Create)

        return (context, institution.Id, teacher.Id);
    }

    [Fact]
    public void Creating_a_governed_entity_through_normal_SaveChanges_produces_exactly_one_audit_row()
    {
        var dbName = nameof(Creating_a_governed_entity_through_normal_SaveChanges_produces_exactly_one_audit_row);
        var actorId = Guid.NewGuid();
        using var context = BuildSeededContext(dbName, actorId).context;

        var rows = context.AuditLogs.Where(a => a.EntityType == nameof(Teacher)).ToList();

        Assert.Single(rows);
        Assert.Equal("Create", rows[0].Action);
        Assert.Equal(actorId, rows[0].ActorUserId);
        Assert.Null(rows[0].BeforeSnapshot);
        Assert.NotNull(rows[0].AfterSnapshot);
        Assert.Contains("Dr. Sarah Smith", rows[0].AfterSnapshot);
    }

    [Fact]
    public void Deactivating_a_governed_entity_is_classified_as_Deactivate_not_a_generic_Update()
    {
        var dbName = nameof(Deactivating_a_governed_entity_is_classified_as_Deactivate_not_a_generic_Update);
        var (context, _, teacherId) = BuildSeededContext(dbName, Guid.NewGuid());
        using (context)
        {
            var teacher = context.Teachers.Single(t => t.Id == teacherId);
            teacher.IsActive = false;
            teacher.DeactivatedAtUtc = DateTime.UtcNow;
            context.SaveChanges();

            var deactivateRow = context.AuditLogs
                .Where(a => a.EntityType == nameof(Teacher) && a.Action == "Deactivate")
                .ToList();
            Assert.Single(deactivateRow);
        }
    }

    [Fact]
    public void ExecuteUpdate_bulk_operation_bypasses_the_interceptor_and_produces_no_audit_row()
    {
        // THE adversarial proof: a mutation path that genuinely exists in EF Core (bulk
        // ExecuteUpdate/ExecuteDelete, introduced in EF Core 7, deliberately does not route
        // through SaveChanges/ChangeTracker at all) silently produces zero audit rows for a
        // governed entity change. This demonstrates the danger is real, not hypothetical.
        //
        // Uses real LocalDB rather than the InMemory provider: InMemory does not implement
        // ExecuteUpdate/ExecuteDelete translation at all (throws InvalidOperationException before
        // ever reaching the database), which would make this test prove nothing about the actual
        // risk — a real relational provider is required to demonstrate the bypass genuinely works
        // and genuinely produces no audit row. (SQLite was tried first but doesn't support SQL
        // Server-style database-generated `rowversion` columns, which several entities rely on.)
        var dbName = $"SchoolSync_AuditBypassTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;")
            .AddInterceptors(new AuditLoggingInterceptor(new FakeAuditContext(Guid.NewGuid())))
            .Options;

        using var context = new SchoolSyncDbContext(options, new FakeCurrentTenant { BypassTenantFilter = true });
        context.Database.EnsureCreated();
        try
        {

            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = "Entry",
                LearnerCapacity = 200,
                StorageAllowanceGb = 5,
                SupportLevel = "Standard",
                CapabilitiesJson = "[]"
            };
            var institution = new Institution
            {
                Id = Guid.NewGuid(),
                Name = "Bypass Audit Test School",
                RegistrationNumber = "REG-BYPASS",
                ContactEmail = "bypass@example.edu",
                Telephone = "+91 90000 03333",
                PostalAddress = "1 St",
                PostalCode = "000003",
                City = "Testville",
                Region = "Test Region",
                SubscriptionPlanId = plan.Id,
                CreatedAtUtc = DateTime.UtcNow
            };
            var teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                InstitutionId = institution.Id,
                Name = "Prof. John Johnson",
                ContactEmail = "john@bypass.edu",
                Telephone = "+91 90000 04444",
                Gender = "Male",
                Address = "2 St",
                JoiningDate = new DateOnly(2020, 1, 5)
            };
            context.SubscriptionPlans.Add(plan);
            context.Institutions.Add(institution);
            context.Teachers.Add(teacher);
            context.SaveChanges();

            var auditRowsBefore = context.AuditLogs.Count(a => a.EntityType == nameof(Teacher));

            context.Teachers.Where(t => t.Id == teacher.Id)
                .ExecuteUpdate(setters => setters.SetProperty(t => t.IsActive, false));

            var auditRowsAfter = context.AuditLogs.Count(a => a.EntityType == nameof(Teacher));

            // The mutation genuinely happened...
            Assert.False(context.Teachers.AsNoTracking().Single(t => t.Id == teacher.Id).IsActive);
            // ...but produced NO audit trail. This is why the codebase must never call
            // ExecuteUpdate/ExecuteDelete on a governed (IAuditable) entity — see the companion
            // static-analysis test below, which proves that this test's own method is the only
            // place in the whole solution where that pattern appears.
            Assert.Equal(auditRowsBefore, auditRowsAfter);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    public void No_production_source_file_calls_ExecuteUpdate_or_ExecuteDelete_on_a_governed_entity()
    {
        // "Demonstrate that path is not exposed in production code" (07-business-logic/_shared.md
        // §3), verified mechanically rather than by promise: scan every .cs file under src/ (this
        // repo's production code, as opposed to tests/) for the two bulk-operation method names.
        // If a future CT ever adds one to make a bulk update "more efficient," this test fails
        // and says exactly why, instead of the gap surviving silently until an audit is missing.
        var repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcDir), $"expected to find src/ under {repoRoot}");

        var offendingFiles = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\.ExecuteUpdate(Async)?\s*\(|\.ExecuteDelete(Async)?\s*\("))
            .ToList();

        Assert.True(offendingFiles.Count == 0,
            "Found ExecuteUpdate/ExecuteDelete in production code, which bypasses the audit " +
            $"interceptor entirely (07-business-logic/_shared.md §3): {string.Join(", ", offendingFiles)}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SchoolSync.slnx"))
                                && !File.Exists(Path.Combine(dir.FullName, "SchoolSync.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test output directory.");
    }
}
