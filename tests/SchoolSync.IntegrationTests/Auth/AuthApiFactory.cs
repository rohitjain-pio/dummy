using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Auth;

// Real ASP.NET pipeline (routing, model binding, [Authorize], JWT bearer validation) end to end
// — an InMemory DbContext swapped in so no real SQL Server is required to run the suite, and a
// fixed test-only signing key so tokens issued in-process validate in-process.
public class AuthApiFactory : WebApplicationFactory<Program>
{
    public readonly Guid TestInstitutionId = Guid.NewGuid();
    private readonly string _dbName = Guid.NewGuid().ToString(); // per-factory-instance isolation

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-only-signing-key-not-for-any-real-environment-at-all",
                ["Jwt:Issuer"] = "SchoolSync.Tests",
                ["Jwt:Audience"] = "SchoolSync.Tests.Clients",
                ["Hangfire:Enabled"] = "false",
                // CT-801 / CFG-SEC-06. The app FAILS TO START without this by design, so every
                // test host must supply one. Test-only value, and deliberately not the JWT key -
                // the two secrets are separate so rotating one cannot silently break the other.
                ["Security:LeadershipCodeLookupKey"] = "test-only-leadership-code-lookup-key-32B+" // CT-402: see SchoolSyncApiFactory
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SchoolSyncDbContext>>();

            // A private internal service provider, scoped to just the InMemory provider's own
            // services — without it, EF Core sees both SqlServer's (from AddInfrastructure) and
            // InMemory's provider services registered in the same root container and refuses to
            // pick one ("Services for database providers ... have been registered").
            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<SchoolSyncDbContext>(options =>
                options.UseInMemoryDatabase(_dbName).UseInternalServiceProvider(inMemoryServiceProvider));
        });
    }

    // Seeds exactly what registration needs (an Institution with a bound SubscriptionPlan) —
    // nothing test-specific beyond that, so each test seeds its own users/state on top.
    public async Task SeedBaselineInstitutionAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Entry",
            LearnerCapacity = 200,
            StorageAllowanceGb = 5,
            SupportLevel = "Standard",
            CapabilitiesJson = "[]"
        };
        db.SubscriptionPlans.Add(plan);
        db.Institutions.Add(new Institution
        {
            Id = TestInstitutionId,
            Name = "Test Institution",
            RegistrationNumber = "REG-T",
            ContactEmail = "t@example.edu",
            Telephone = "+91 90000 09999",
            PostalAddress = "1 Test St",
            PostalCode = "000001",
            City = "Testville",
            Region = "Test Region",
            SubscriptionPlanId = plan.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
