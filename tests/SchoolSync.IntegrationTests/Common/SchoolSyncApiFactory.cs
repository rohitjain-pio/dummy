using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Common;

// The shared host used by CT-102 onward. CT-101's InstitutionsApiFactory pre-dates this type and
// still carries its own copy of the same host wiring; it is left untouched because it belongs to
// an already-completed CT, and folding it into this base is a CT-106-or-later cleanup, not a
// change to make while its own phase gate is still open.
public class SchoolSyncApiFactory : WebApplicationFactory<Program>
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
                // CT-402: Hangfire keeps its storage in static state, which parallel test hosts would
                // share; HTTP test hosts run with it off and substitute their own job queue.
                ["Hangfire:Enabled"] = "false",
                // CT-801 / CFG-SEC-06. The app FAILS TO START without this by design, so every
                // test host must supply one. Test-only value, and deliberately not the JWT key -
                // the two secrets are separate so rotating one cannot silently break the other.
                ["Security:LeadershipCodeLookupKey"] = "test-only-leadership-code-lookup-key-32B+"
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

    // EF Core's HasData seed only materializes for the InMemory provider once the database is
    // explicitly created — unlike SqlServer, where `dotnet ef database update` applies it via a
    // migration. (Found the hard way in CT-101.)
    public async Task EnsureDatabaseSeededAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>().Database.EnsureCreatedAsync();
    }

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

    public async Task<Guid> SeedInstitutionAsync(string name = "Seeded School")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = name,
            RegistrationNumber = $"REG-{Guid.NewGuid():N}",
            ContactEmail = $"{Guid.NewGuid():N}@example.edu",
            Telephone = "+919000000000",
            PostalAddress = "1 Seeded St",
            PostalCode = "560001",
            City = "Seed City",
            Region = "Seed Region",
            SubscriptionPlanId = SchoolSyncDbContext.EntryPlanId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();
        return institution.Id;
    }

    // CT-503: move an institution to another subscription plan (e.g. Bespoke for ParentAccess).
    public async Task SetPlanAsync(Guid institutionId, Guid planId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var institution = await db.Institutions.SingleAsync(i => i.Id == institutionId);
        institution.SubscriptionPlanId = planId;
        await db.SaveChangesAsync();
    }

    // Writes bypass the tenant query filter by nature (filters apply to reads), so a seed can set
    // InstitutionId explicitly without needing an authenticated context.
    public async Task<Guid> SeedClassAsync(Guid institutionId, string name = "Grade 5", string section = "A")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var @class = new Class { Id = Guid.NewGuid(), InstitutionId = institutionId, Name = name, Section = section };
        db.Classes.Add(@class);
        await db.SaveChangesAsync();
        return @class.Id;
    }

    public async Task<Guid> SeedSubjectAsync(Guid institutionId, string name = "Mathematics")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var subject = new Subject { Id = Guid.NewGuid(), InstitutionId = institutionId, Name = name };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject.Id;
    }

    public async Task<Guid> SeedUserAccountAsync(Guid institutionId, UserRole role)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            Role = role,
            Username = $"user-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.edu",
            PasswordHash = "seeded-not-a-real-hash",
            IsActive = true
        };
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public async Task<Guid> SeedStudentAsync(Guid institutionId, Guid classId, string srNumber, string rollNumber)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            ClassId = classId,
            SrNumber = srNumber,
            RollNumber = rollNumber,
            Name = "Seeded Student",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
            Gender = "Female",
            Email = $"{Guid.NewGuid():N}@example.edu",
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }
}
