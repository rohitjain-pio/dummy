using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Performance;

/// <summary>
/// CT-701. The NFR performance host, and the ONLY test host that runs on a real SQL Server.
///
/// Every other factory in this suite uses the EF Core InMemory provider, which is right for
/// behavioural tests and worthless for these: InMemory has no query planner, no indexes and no
/// I/O, so a p95 measured against it would say nothing at all about NFR-006/007/022. The
/// 14-test-spec §6 method column says "load test, realistic dataset size" — that can only be read
/// as a real database. This host therefore migrates a throwaway LocalDB from zero, exactly as the
/// per-phase migration check does, and the dataset is seeded through the real schema with the real
/// indexes in place.
/// </summary>
public sealed class PerfApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"SchoolSync_Perf_{Guid.NewGuid():N}";
    public string ConnectionString => $@"Server=(localdb)\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "test-only-signing-key-not-for-any-real-environment-at-all",
            ["Jwt:Issuer"] = "SchoolSync.Tests",
            ["Jwt:Audience"] = "SchoolSync.Tests.Clients",
            ["Hangfire:Enabled"] = "false",
                // CT-801 / CFG-SEC-06. The app FAILS TO START without this by design, so every
                // test host must supply one. Test-only value, and deliberately not the JWT key -
                // the two secrets are separate so rotating one cannot silently break the other.
                ["Security:LeadershipCodeLookupKey"] = "test-only-leadership-code-lookup-key-32B+",
            ["ConnectionStrings:SchoolSync"] = ConnectionString
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SchoolSyncDbContext>>();
            services.AddDbContext<SchoolSyncDbContext>(o => o.UseSqlServer(ConnectionString));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        // Migrate, never EnsureCreated: the point is to measure against the schema the product
        // actually ships, indexes and all.
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    public SchoolSyncDbContext NewDbContext(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();

    public string IssueTokenFor(Guid userId, UserRole role, Guid institutionId)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().IssueAccessToken(new UserAccount
        {
            Id = userId,
            Role = role,
            InstitutionId = institutionId,
            Username = $"perf-{userId:N}",
            Email = $"perf-{userId:N}@example.edu",
            PasswordHash = "unused"
        });
    }
}
