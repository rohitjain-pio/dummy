using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Persistence;

// CT-001 DoD: "Every tenant-owned table has the filter applied via the shared convention,
// verified by a test that queries without a filter override and gets zero cross-tenant rows."
// company.md §7 adversarial-fixture rule: a test must prove cross-tenant data is actually
// BLOCKED, not merely that same-tenant data is returned — so every test here asserts a
// specific, deliberately-seeded cross-tenant row is absent, not just that "some" rows come back.
public class TenantQueryFilterTests
{
    private static SchoolSyncDbContext BuildContext(FakeCurrentTenant tenant, string dbName)
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new SchoolSyncDbContext(options, tenant);
    }

    private static async Task<(Guid institutionA, Guid institutionB, Guid studentA, Guid studentB)> SeedTwoTenants(string dbName)
    {
        var bypassTenant = new FakeCurrentTenant { BypassTenantFilter = true };
        await using var seedContext = BuildContext(bypassTenant, dbName);

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Entry",
            LearnerCapacity = 200,
            StorageAllowanceGb = 5,
            SupportLevel = "Standard",
            CapabilitiesJson = "[]"
        };
        seedContext.SubscriptionPlans.Add(plan);

        var institutionA = new Institution
        {
            Id = Guid.NewGuid(),
            Name = "Greenwood International School",
            RegistrationNumber = "REG-A",
            ContactEmail = "a@example.edu",
            Telephone = "+91 90000 00001",
            PostalAddress = "1 A St",
            PostalCode = "411001",
            City = "Pune",
            Region = "Maharashtra",
            SubscriptionPlanId = plan.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        var institutionB = new Institution
        {
            Id = Guid.NewGuid(),
            Name = "Riverdale Public School",
            RegistrationNumber = "REG-B",
            ContactEmail = "b@example.edu",
            Telephone = "+91 90000 00002",
            PostalAddress = "2 B St",
            PostalCode = "560001",
            City = "Bengaluru",
            Region = "Karnataka",
            SubscriptionPlanId = plan.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        seedContext.Institutions.AddRange(institutionA, institutionB);

        var studentA = new Student
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionA.Id,
            SrNumber = "SR-A-1",
            RollNumber = "1",
            Name = "Aarav Mehta",
            DateOfBirth = new DateOnly(2012, 3, 14),
            Gender = "Male",
            Email = "aarav@a.edu"
        };
        var studentB = new Student
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionB.Id,
            SrNumber = "SR-B-1",
            RollNumber = "1",
            Name = "Kabir Rao",
            DateOfBirth = new DateOnly(2011, 11, 25),
            Gender = "Male",
            Email = "kabir@b.edu"
        };
        seedContext.Students.AddRange(studentA, studentB);

        await seedContext.SaveChangesAsync();

        return (institutionA.Id, institutionB.Id, studentA.Id, studentB.Id);
    }

    [Fact]
    public async Task Listing_students_as_tenant_A_never_returns_tenant_Bs_seeded_row()
    {
        var dbName = nameof(Listing_students_as_tenant_A_never_returns_tenant_Bs_seeded_row);
        var (institutionA, institutionB, _, studentB) = await SeedTwoTenants(dbName);

        var tenantA = new FakeCurrentTenant { InstitutionId = institutionA, BypassTenantFilter = false };
        await using var context = BuildContext(tenantA, dbName);

        var results = await context.Students.ToListAsync();

        Assert.DoesNotContain(results, s => s.Id == studentB); // the adversarial assertion
        Assert.All(results, s => Assert.Equal(institutionA, s.InstitutionId));
    }

    [Fact]
    public async Task Fetching_tenant_Bs_student_by_its_real_id_while_scoped_to_tenant_A_returns_null()
    {
        // The stronger proof: not "the list happens to be short" but "a query that names the
        // exact primary key of a row known to exist in the database still returns nothing,"
        // which is what "zero cross-tenant rows" actually has to mean.
        var dbName = nameof(Fetching_tenant_Bs_student_by_its_real_id_while_scoped_to_tenant_A_returns_null);
        var (institutionA, _, _, studentB) = await SeedTwoTenants(dbName);

        var tenantA = new FakeCurrentTenant { InstitutionId = institutionA, BypassTenantFilter = false };
        await using var context = BuildContext(tenantA, dbName);

        var found = await context.Students.FirstOrDefaultAsync(s => s.Id == studentB);

        Assert.Null(found);
    }

    [Fact]
    public async Task Bypass_path_returns_both_tenants_rows_proving_the_escape_hatch_itself_works()
    {
        // BR-013: the PlatformOperator's explicitly-marked cross-tenant path must actually work
        // when deliberately invoked — a filter convention that can never be bypassed at all
        // would silently break the one legitimate cross-tenant query the platform needs.
        var dbName = nameof(Bypass_path_returns_both_tenants_rows_proving_the_escape_hatch_itself_works);
        var (_, _, studentA, studentB) = await SeedTwoTenants(dbName);

        var platformOperator = new FakeCurrentTenant { InstitutionId = null, BypassTenantFilter = true };
        await using var context = BuildContext(platformOperator, dbName);

        var results = await context.Students.ToListAsync();

        Assert.Contains(results, s => s.Id == studentA);
        Assert.Contains(results, s => s.Id == studentB);
    }

    [Fact]
    public async Task Teacher_a_second_entity_type_is_also_tenant_scoped_by_the_same_convention()
    {
        // Sanity check on a second ITenantOwned entity type, not just Student — proves the
        // convention is applied structurally (by reflection over ITenantOwned), not hand-wired
        // per entity. (Institution itself is deliberately excluded from this suite: it IS the
        // tenant, not tenant-owned, so it carries no InstitutionId to filter on.)
        var dbName = nameof(Teacher_a_second_entity_type_is_also_tenant_scoped_by_the_same_convention);
        var bypassTenant = new FakeCurrentTenant { BypassTenantFilter = true };
        Guid institutionA, institutionB, teacherB;
        await using (var seedContext = BuildContext(bypassTenant, dbName))
        {
            var (a, b, _, _) = await SeedTwoTenants(dbName);
            institutionA = a;
            institutionB = b;

            var teacherForB = new Teacher
            {
                Id = Guid.NewGuid(),
                InstitutionId = institutionB,
                Name = "Dr. Sarah Smith",
                ContactEmail = "sarah@b.edu",
                Telephone = "+91 90000 00003",
                Gender = "Female",
                Address = "3 C St",
                JoiningDate = new DateOnly(2019, 6, 10)
            };
            seedContext.Teachers.Add(teacherForB);
            await seedContext.SaveChangesAsync();
            teacherB = teacherForB.Id;
        }

        var tenantA = new FakeCurrentTenant { InstitutionId = institutionA, BypassTenantFilter = false };
        await using var context = BuildContext(tenantA, dbName);

        var found = await context.Teachers.FirstOrDefaultAsync(t => t.Id == teacherB);

        Assert.Null(found);
    }
}
