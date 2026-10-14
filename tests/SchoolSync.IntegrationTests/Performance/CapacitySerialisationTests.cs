using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SchoolSync.IntegrationTests.Performance;

/// <summary>
/// CT-701 / U29 — the learner-capacity gate is check-then-act, and 04-data-model.md §4's
/// concurrency policy does not cover it. Its four documented races each resolve at a unique index,
/// because each is about one row's identity; "COUNT(active students) &lt;= plan capacity" is an
/// aggregate invariant that no constraint can express, so it has to be serialised instead.
///
/// This is deliberately a REAL SQL Server test. The rest of the suite runs on the InMemory
/// provider, which has no isolation levels and no locking, so it cannot fail this test and cannot
/// prove it either. Run it on InMemory and the race is invisible; that is precisely how U29
/// survived five phase gates.
/// </summary>
[Collection(nameof(PerfCollection))]
public sealed class CapacitySerialisationTests(PerfApiFactory factory, ITestOutputHelper output)
{
    private const int EntryPlanCapacity = 200; // 04-data-model.md §2.10 seed

    [Fact]
    public async Task Concurrent_admissions_at_the_last_seat_admit_exactly_one()
    {
        const int contenders = 8;

        // An Entry-plan institution with exactly one seat left.
        var (institutionId, leadershipUserId, classId) = await SeedAtCapacityMinusOneAsync();

        var bodies = Enumerable.Range(0, contenders).Select(i => new
        {
            srNumber = "RACE-SR-" + i,
            rollNumber = "RACE-R-" + i,
            name = "Race Contender " + i,
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
            gender = "Female",
            email = "race" + i + "." + Guid.NewGuid().ToString("N")[..6] + "@example.edu",
            classId
        }).ToList();

        // All fired together, each on its own client, so they genuinely interleave in the server.
        var responses = await Task.WhenAll(bodies.Select(async body =>
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", factory.IssueTokenFor(leadershipUserId, UserRole.Principal, institutionId));
            return await client.PostAsJsonAsync("/api/v1/students", body);
        }));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refused = responses.Count(r => r.StatusCode == HttpStatusCode.PaymentRequired); // ERR-801
        var other = responses.Where(r => r.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.PaymentRequired))
                             .Select(r => r.StatusCode.ToString()).ToList();

        output.WriteLine($"{contenders} concurrent admissions at the last seat -> {created} created, {refused} refused with ERR-801, other: [{string.Join(", ", other)}]");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var active = await db.Students.IgnoreQueryFilters()
            .CountAsync(s => s.InstitutionId == institutionId && s.IsActive);
        output.WriteLine($"active learners after the race: {active} (plan capacity {EntryPlanCapacity})");

        // The invariant is the point. One seat was free, so one admission may succeed.
        Assert.Empty(other);
        Assert.Equal(1, created);
        Assert.Equal(contenders - 1, refused);
        Assert.Equal(EntryPlanCapacity, active);
        Assert.True(active <= EntryPlanCapacity, $"BRL-073 breached: {active} active learners against a capacity of {EntryPlanCapacity}");
    }

    private async Task<(Guid InstitutionId, Guid LeadershipUserId, Guid ClassId)> SeedAtCapacityMinusOneAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();

        var institutionId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        db.Institutions.Add(new Institution
        {
            Id = institutionId,
            Name = "Capacity Race School",
            RegistrationNumber = "REG-" + suffix,
            ContactEmail = suffix + "@example.edu",
            Telephone = "+919000000000",
            PostalAddress = "1 Race St",
            PostalCode = "560001",
            City = "Race City",
            Region = "Race Region",
            SubscriptionPlanId = SchoolSyncDbContext.EntryPlanId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        });

        var leadershipUserId = Guid.NewGuid();
        db.UserAccounts.Add(new UserAccount
        {
            Id = leadershipUserId,
            InstitutionId = institutionId,
            Role = UserRole.Principal,
            Username = "head-" + suffix,
            Email = "head-" + suffix + "@example.edu",
            PasswordHash = "seeded-not-a-real-hash",
            IsActive = true
        });

        var classId = Guid.NewGuid();
        db.Classes.Add(new Class { Id = classId, InstitutionId = institutionId, Name = "Grade 1", Section = "A" });

        for (var i = 0; i < EntryPlanCapacity - 1; i++)
        {
            db.Students.Add(new Student
            {
                Id = Guid.NewGuid(),
                InstitutionId = institutionId,
                ClassId = classId,
                SrNumber = "SEED-SR-" + i.ToString("D4"),
                RollNumber = "SEED-R-" + i.ToString("D4"),
                Name = "Seeded " + i.ToString("D4"),
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
                Gender = i % 2 == 0 ? "Female" : "Male",
                Email = "seed" + i.ToString("D4") + "." + suffix + "@example.edu",
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (institutionId, leadershipUserId, classId);
    }
}
