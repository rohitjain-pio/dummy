using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SchoolSync.IntegrationTests.Performance;

/// <summary>
/// CT-701 — the NFR performance checks. 14-test-spec/_shared.md §6.
///
/// Every target here is MEASURED against the realistic dataset and the measured number is printed,
/// not merely asserted: the DoD is "every NFR numeric target measured at least once against a
/// realistic dataset", and a bare green tick would not satisfy it. The assertions are the targets
/// from §6; the printed p95s are the evidence.
///
/// These run on a real LocalDB (see PerfApiFactory) and are collection-isolated so they never run
/// beside the InMemory suites.
/// </summary>
[Collection(nameof(PerfCollection))]
public sealed class NfrPerformanceTests(PerfApiFactory factory, ITestOutputHelper output)
{
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 25;

    private HttpClient ClientFor(Guid userId, UserRole role, Guid institutionId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.IssueTokenFor(userId, role, institutionId));
        return client;
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        var ordered = samples.OrderBy(x => x).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * ordered.Count) - 1;
        return ordered[Math.Clamp(rank, 0, ordered.Count - 1)];
    }

    private async Task<double> MeasureP95Async(string label, Func<Task> action)
    {
        for (var i = 0; i < WarmupIterations; i++) await action();

        var samples = new List<double>(MeasuredIterations);
        for (var i = 0; i < MeasuredIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        var p95 = Percentile(samples, 95);
        output.WriteLine($"{label}: p50={Percentile(samples, 50):F1}ms p95={p95:F1}ms max={samples.Max():F1}ms over {MeasuredIterations} iterations");
        return p95;
    }

    // ── NFR-006 — standard list and detail views return within 2 seconds under normal load ──────
    [Fact]
    public async Task Nfr006_list_and_detail_views_p95_under_2000ms()
    {
        var seeded = await RealisticDataset.SeedAsync(factory, "NFR-006 School");
        var client = ClientFor(seeded.LeadershipUserId, UserRole.Principal, seeded.InstitutionId);
        output.WriteLine($"dataset: {seeded.StudentIds.Count} students, {seeded.ClassIds.Count} classes, {seeded.AttendanceRowCount} attendance rows");

        // Every measurement asserts the PAYLOAD, not just the status. The first version of this
        // file asserted only 200 OK and reported a 0.5ms p95 for a 1,000-row list — because the
        // URLs were missing the api/v1 prefix and every request was being served the SPA fallback
        // index.html. A perf test that cannot tell 1,000 rows from a static HTML file measures
        // nothing, so the row counts below are part of the measurement, not decoration.
        var listP95 = await MeasureP95Async("NFR-006 GET /students (list, 1000 rows)", async () =>
        {
            var response = await client.GetAsync("/api/v1/students");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Equal(seeded.StudentIds.Count, body!.Count);
        });

        var detailP95 = await MeasureP95Async("NFR-006 GET /students/{id} (detail)", async () =>
        {
            var response = await client.GetAsync($"/api/v1/students/{seeded.FirstStudentId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.Equal(seeded.FirstStudentId, Guid.Parse(body!["id"].ToString()!));
        });

        var rosterP95 = await MeasureP95Async("NFR-006 GET /classes/{id}/roster", async () =>
        {
            var response = await client.GetAsync($"/api/v1/classes/{seeded.FirstClassId}/roster");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Equal(RealisticDataset.StudentsPerClass, body!.Count);
        });

        Assert.True(listP95 < 2000, $"NFR-006 list p95 was {listP95:F1}ms, target <2000ms");
        Assert.True(detailP95 < 2000, $"NFR-006 detail p95 was {detailP95:F1}ms, target <2000ms");
        Assert.True(rosterP95 < 2000, $"NFR-006 roster p95 was {rosterP95:F1}ms, target <2000ms");
    }

    // ── NFR-007 — dashboard summaries render within 3 seconds ───────────────────────────────────
    [Fact]
    public async Task Nfr007_dashboard_p95_under_3000ms()
    {
        var seeded = await RealisticDataset.SeedAsync(factory, "NFR-007 School");
        var client = ClientFor(seeded.LeadershipUserId, UserRole.Principal, seeded.InstitutionId);

        // §6 asks for warm AND cold. The first call after seeding is the cold one.
        var coldSw = Stopwatch.StartNew();
        var cold = await client.GetAsync("/api/v1/dashboard");
        coldSw.Stop();
        Assert.Equal(HttpStatusCode.OK, cold.StatusCode);
        var coldBody = await cold.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotEmpty(coldBody!); // a real payload, not the SPA fallback
        output.WriteLine($"NFR-007 GET /dashboard (cold): {coldSw.Elapsed.TotalMilliseconds:F1}ms");

        var warmP95 = await MeasureP95Async("NFR-007 GET /dashboard (warm)", async () =>
        {
            var response = await client.GetAsync("/api/v1/dashboard");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.NotEmpty(body!);
        });

        Assert.True(coldSw.Elapsed.TotalMilliseconds < 3000, $"NFR-007 cold was {coldSw.Elapsed.TotalMilliseconds:F1}ms, target <3000ms");
        Assert.True(warmP95 < 3000, $"NFR-007 warm p95 was {warmP95:F1}ms, target <3000ms");
    }

    // ── NFR-008 — chat messages delivered to all present participants within 1 second ───────────
    [Fact]
    public async Task Nfr008_chat_fanout_to_all_participants_p95_under_1000ms()
    {
        const int participants = 25; // N present in one room; each is a real SignalR connection
        var seeded = await RealisticDataset.SeedAsync(factory, "NFR-008 School", classes: 4, attendanceDays: 1);

        // Seed N members of the room, then connect each one for real.
        var memberIds = new List<Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
            for (var i = 0; i < participants; i++)
            {
                var userId = Guid.NewGuid();
                memberIds.Add(userId);
                db.UserAccounts.Add(new UserAccount
                {
                    Id = userId,
                    InstitutionId = seeded.InstitutionId,
                    Role = UserRole.Teacher,
                    Username = "chat-" + i.ToString("D3") + "-" + Guid.NewGuid().ToString("N")[..6],
                    Email = "chat" + i + "." + Guid.NewGuid().ToString("N")[..6] + "@example.edu",
                    PasswordHash = "seeded-not-a-real-hash",
                    IsActive = true
                });
                db.RoomMemberships.Add(new RoomMembership
                {
                    RoomId = seeded.RoomId,
                    UserId = userId,
                    InstitutionId = seeded.InstitutionId,
                    JoinedAtUtc = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        var server = factory.Server;
        var connections = new List<HubConnection>();
        var received = new List<TaskCompletionSource<DateTime>>();

        try
        {
            foreach (var userId in memberIds)
            {
                var token = factory.IssueTokenFor(userId, UserRole.Teacher, seeded.InstitutionId);
                var connection = new HubConnectionBuilder()
                    .WithUrl(new Uri(server.BaseAddress, SchoolSync.Application.Communication.RoomService.HubPath.TrimStart('/')), o =>
                    {
                        o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                        o.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    })
                    .Build();
                connections.Add(connection);
                await connection.StartAsync();
                await connection.InvokeAsync("JoinRoom", seeded.RoomId);
            }

            var samples = new List<double>();
            for (var round = 0; round < 10; round++)
            {
                received = connections.Select(_ => new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously)).ToList();
                var handlers = new List<IDisposable>();
                for (var i = 0; i < connections.Count; i++)
                {
                    var tcs = received[i];
                    handlers.Add(connections[i].On<object>("MessageSent", _ => tcs.TrySetResult(DateTime.UtcNow)));
                }

                var sw = Stopwatch.StartNew();
                await connections[0].InvokeAsync<object>("SendMessage", seeded.RoomId, Guid.NewGuid(), "perf round " + round);
                await Task.WhenAll(received.Select(r => r.Task)).WaitAsync(TimeSpan.FromSeconds(10));
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);

                foreach (var handler in handlers) handler.Dispose();
            }

            var p95 = Percentile(samples, 95);
            output.WriteLine($"NFR-008 chat fan-out to {participants} present participants: p50={Percentile(samples, 50):F1}ms p95={p95:F1}ms max={samples.Max():F1}ms over {samples.Count} rounds");
            Assert.True(p95 < 1000, $"NFR-008 fan-out p95 was {p95:F1}ms to {participants} participants, target <1000ms");
        }
        finally
        {
            foreach (var connection in connections) await connection.DisposeAsync();
        }
    }

    // ── NFR-022 — attendance-capture peak at the start of the school day, no degradation ────────
    [Fact]
    public async Task Nfr022_attendance_peak_no_degradation()
    {
        var seeded = await RealisticDataset.SeedAsync(factory, "NFR-022 School");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Each class is marked by ITS OWN class teacher, which is the real shape of the school-day
        // peak and also puts the per-request BR-020 authorization lookups under the same load.
        async Task<double> MarkClassAsync(Guid classId, DateOnly date)
        {
            var client = ClientFor(seeded.ClassTeacherUserIds[classId], UserRole.Teacher, seeded.InstitutionId);
            var rosterResponse = await client.GetAsync($"/api/v1/classes/{classId}/roster");
            rosterResponse.EnsureSuccessStatusCode();
            var roster = await rosterResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? [];
            var entries = roster.Select(r => new
            {
                personId = Guid.Parse(r["id"].ToString()!),
                personType = "Student",
                outcome = "Present"
            }).ToArray();

            var sw = Stopwatch.StartNew();
            var response = await client.PutAsJsonAsync("/api/v1/attendance", new { classId, date, entries });
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return sw.Elapsed.TotalMilliseconds;
        }

        // Baseline: one class marking on its own.
        var baseline = await MarkClassAsync(seeded.ClassIds[0], today);
        output.WriteLine($"NFR-022 baseline single-class bulk mark: {baseline:F1}ms");

        // Peak: every remaining class submits concurrently, as at school-day start.
        var peakClasses = seeded.ClassIds.Skip(1).ToList();
        var peakSw = Stopwatch.StartNew();
        var durations = await Task.WhenAll(peakClasses.Select(c => MarkClassAsync(c, today)));
        peakSw.Stop();

        var peakP95 = Percentile(durations.ToList(), 95);
        output.WriteLine($"NFR-022 peak: {peakClasses.Count} classes concurrently, wall clock {peakSw.Elapsed.TotalMilliseconds:F1}ms, per-request p50={Percentile(durations.ToList(), 50):F1}ms p95={peakP95:F1}ms");

        // "No degradation" is read as: under peak, a bulk mark still meets the NFR-006 interactive
        // ceiling. A stricter multiple-of-baseline rule would measure this machine's core count,
        // not the product.
        Assert.True(peakP95 < 2000, $"NFR-022 per-request p95 under peak was {peakP95:F1}ms, target <2000ms");

        // And every class's register actually landed — throughput without correctness is nothing.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        var marked = await db.AttendanceRecords.IgnoreQueryFilters()
            .CountAsync(a => a.InstitutionId == seeded.InstitutionId && a.AttendanceDate == today);
        Assert.Equal(seeded.StudentIds.Count, marked);
    }

    // ── NFR-020 — many independent institutions concurrently, no leakage or contention ──────────
    [Fact]
    public async Task Nfr020_fifty_concurrent_institutions_no_leakage_or_contention()
    {
        const int institutions = 50;
        var seeds = new List<RealisticDataset.Seeded>();
        for (var i = 0; i < institutions; i++)
        {
            // Small per-institution datasets: this scenario measures concurrency and isolation at
            // N=50, not per-institution volume (NFR-006 already measures that at full size).
            seeds.Add(await RealisticDataset.SeedAsync(factory, "Tenant " + i.ToString("D2"), classes: 2, attendanceDays: 1));
        }

        var sw = Stopwatch.StartNew();
        var results = await Task.WhenAll(seeds.Select(async seed =>
        {
            var client = ClientFor(seed.LeadershipUserId, UserRole.Principal, seed.InstitutionId);
            var response = await client.GetAsync("/api/v1/students");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var students = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? [];
            var ids = students.Select(s => Guid.Parse(s["id"].ToString()!)).ToHashSet();
            return (seed, ids);
        }));
        sw.Stop();
        output.WriteLine($"NFR-020 {institutions} institutions queried concurrently in {sw.Elapsed.TotalMilliseconds:F1}ms");

        foreach (var (seed, ids) in results)
        {
            // Exactly its own learners: no more (leakage) and no fewer (contention dropping rows).
            Assert.Equal(seed.StudentIds.Count, ids.Count);
            Assert.True(ids.SetEquals(seed.StudentIds), $"institution {seed.InstitutionId} saw a student set that was not its own");
        }
    }
}

[CollectionDefinition(nameof(PerfCollection))]
public sealed class PerfCollection : ICollectionFixture<PerfApiFactory>;
