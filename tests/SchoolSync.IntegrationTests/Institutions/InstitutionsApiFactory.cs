using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Institutions;

public class InstitutionsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

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
            var inMemoryServiceProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<SchoolSyncDbContext>(options =>
                options.UseInMemoryDatabase(_dbName).UseInternalServiceProvider(inMemoryServiceProvider));
        });
    }

    // EF Core's model-level HasData seed (the 3 SubscriptionPlan rows, BR-061) only materializes
    // for the InMemory provider once the database is explicitly created — unlike SqlServer,
    // where `dotnet ef database update` applies it via a migration. Call once before any test in
    // this class runs.
    public async Task EnsureDatabaseSeededAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>().Database.EnsureCreatedAsync();
    }

    // No public path issues a PlatformOperator JWT (Q-202: self-registration excludes that role
    // entirely) — tests mint one directly via the same ITokenService the app uses, which is
    // exactly what "provisioned only via an internal path" means in practice.
    public string IssueTokenFor(Guid userId, UserRole role, Guid institutionId)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.IssueAccessToken(new UserAccount
        {
            Id = userId,
            Role = role,
            InstitutionId = institutionId,
            Username = $"test-{userId:N}",
            Email = $"test-{userId:N}@example.edu",
            PasswordHash = "unused"
        });
    }

    public async Task<Guid> SeedInstitutionAsync(string name = "Existing School", bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = name,
            RegistrationNumber = $"REG-{Guid.NewGuid():N}",
            ContactEmail = $"{Guid.NewGuid():N}@example.edu",
            Telephone = "+91 90000 05555",
            PostalAddress = "1 Existing St",
            PostalCode = "000009",
            City = "Existing City",
            Region = "Existing Region",
            SubscriptionPlanId = SchoolSyncDbContext.EntryPlanId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = isActive
        };
        db.Institutions.Add(institution);
        db.PublicInstitutionDirectory.Add(new PublicInstitutionDirectoryProjection
        {
            InstitutionId = institution.Id,
            Name = institution.Name,
            City = institution.City,
            Region = institution.Region
        });
        await db.SaveChangesAsync();
        return institution.Id;
    }
}
