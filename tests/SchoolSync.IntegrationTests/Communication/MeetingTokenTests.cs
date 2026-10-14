using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Communication;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Common;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Communication;

// CT-304 — API-047. 14-test-spec/communication.md TC-507 (Contract), plus the Q-220 meeting
// lifecycle and Q-221's "ACS not configured" behaviour. TC-508/509 are browser tests and move to
// CT-603 with the meeting UI (Q-218).
public class MeetingTokenTests : IAsyncLifetime
{
    private const string Passcode = "cedar-2290";

    // Counts every token request so a test can prove none was minted.
    private sealed class CountingIssuer : IMeetingTokenIssuer
    {
        public int Calls;
        public Task<IssuedAcsToken?> IssueAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult<IssuedAcsToken?>(new IssuedAcsToken($"fake-acs-token-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(1)));
        }
    }

    private sealed class AcsConfiguredFactory(CountingIssuer issuer) : SchoolSyncApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMeetingTokenIssuer>();
                services.AddSingleton<IMeetingTokenIssuer>(issuer);
            });
        }
    }

    private readonly CountingIssuer _issuer = new();
    private SchoolSyncApiFactory _factory = null!;
    private Guid _institutionId;
    private Guid _roomId;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory = new AcsConfiguredFactory(_issuer);
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        var created = await RestAs(_aliceId, UserRole.Teacher).PostAsJsonAsync("/api/v1/rooms", new { name = "Staff Room", passcode = Passcode });
        _roomId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await RestAs(_bobId, UserRole.Teacher).PostAsJsonAsync($"/api/v1/rooms/{_roomId}/join", new { passcode = Passcode });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient RestAs(Guid userId, UserRole role, Guid? institutionId = null, SchoolSyncApiFactory? factory = null)
    {
        var f = factory ?? _factory;
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", f.IssueTokenFor(userId, role, institutionId ?? _institutionId));
        return client;
    }

    private Task<HttpResponseMessage> RequestTokenAsync(HttpClient client, Guid? roomId = null) =>
        client.PostAsync($"/api/v1/rooms/{roomId ?? _roomId}/meetings/token", null);

    private Task<List<Meeting>> MeetingsAsync() => WithDbAsync(db => db.Meetings.Where(m => m.RoomId == _roomId).ToListAsync());

    private async Task<T> WithDbAsync<T>(Func<SchoolSyncDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>());
    }

    [Fact] // TC-507 — adversarial: a non-member gets ERR-505 and NO ACS token is issued
    public async Task A_non_member_is_refused_with_ERR_505_and_no_token_or_meeting_is_created()
    {
        var outsider = RestAs(Guid.NewGuid(), UserRole.Principal); // leadership, but not in the room

        var response = await RequestTokenAsync(outsider);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("ERR-505", raw);
        Assert.DoesNotContain("acsToken", raw);
        Assert.Equal(0, _issuer.Calls); // the identity service was never called
        Assert.Empty(await MeetingsAsync());
    }

    [Fact] // PF-009 happy path + Q-220: members get tokens for the SAME live meeting
    public async Task Two_members_receive_tokens_for_the_same_live_meeting()
    {
        var alice = await (await RequestTokenAsync(RestAs(_aliceId, UserRole.Teacher))).Content.ReadFromJsonAsync<JsonElement>();
        var bobResponse = await RequestTokenAsync(RestAs(_bobId, UserRole.Teacher));

        Assert.Equal(HttpStatusCode.OK, bobResponse.StatusCode);
        var bob = await bobResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("fake-acs-token-", bob.GetProperty("acsToken").GetString());
        Assert.True(bob.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow);
        Assert.NotEqual(alice.GetProperty("acsToken").GetString(), bob.GetProperty("acsToken").GetString()); // per participant
        Assert.Equal(alice.GetProperty("groupCallId").GetGuid(), bob.GetProperty("groupCallId").GetGuid()); // one call
        var live = Assert.Single(await MeetingsAsync());
        Assert.Null(live.EndedAtUtc);
        Assert.Equal(live.Id, bob.GetProperty("groupCallId").GetGuid());
    }

    [Fact] // Q-221: with no ACS configured, the endpoint fails visibly and starts no meeting
    public async Task Without_ACS_configured_a_member_gets_503_ERR_903_and_no_meeting_starts()
    {
        using var unconfigured = new SchoolSyncApiFactory(); // default registration: no ACS_CONNECTION_STRING
        await unconfigured.EnsureDatabaseSeededAsync();
        var institution = await unconfigured.SeedInstitutionAsync();
        var creator = Guid.NewGuid();
        var client = RestAs(creator, UserRole.Teacher, institution, unconfigured);
        var room = (await (await client.PostAsJsonAsync("/api/v1/rooms", new { name = "R", passcode = "p" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await RequestTokenAsync(client, room);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("ERR-903", await response.Content.ReadAsStringAsync());
        using var scope = unconfigured.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>().Meetings.ToListAsync());
    }

    [Fact] // Q-220: removing the room ends its live meeting, and the token endpoint then 404s
    public async Task Removing_the_room_ends_the_live_meeting()
    {
        await RequestTokenAsync(RestAs(_aliceId, UserRole.Teacher));

        Assert.Equal(HttpStatusCode.NoContent, (await RestAs(_aliceId, UserRole.Teacher).DeleteAsync($"/api/v1/rooms/{_roomId}")).StatusCode);

        Assert.NotNull(Assert.Single(await MeetingsAsync()).EndedAtUtc);
        Assert.Equal(HttpStatusCode.NotFound, (await RequestTokenAsync(RestAs(_bobId, UserRole.Teacher))).StatusCode);
    }

    [Fact] // Tenant isolation: another institution cannot request a token for this room
    public async Task Another_institution_gets_404()
    {
        var other = await _factory.SeedInstitutionAsync("Other School");
        Assert.Equal(HttpStatusCode.NotFound, (await RequestTokenAsync(RestAs(_aliceId, UserRole.Teacher, other))).StatusCode);
        Assert.Equal(0, _issuer.Calls);
    }
}

// Q-220's other end condition, at the service layer: the meeting ends when the LAST member leaves.
public class MeetingLifecycleServiceTests : IDisposable
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid AliceId = Guid.NewGuid();
    private static readonly Guid BobId = Guid.NewGuid();
    private readonly SchoolSyncDbContext _db;
    private readonly MessagingService _messaging;

    public MeetingLifecycleServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new SchoolSyncDbContext(options, new FakeCurrentTenant { InstitutionId = InstitutionId });
        _db.Rooms.Add(new Room { Id = RoomId, InstitutionId = InstitutionId, Name = "R", PasscodeHash = "x", CreatedByUserId = AliceId });
        _db.RoomMemberships.AddRange(
            new RoomMembership { RoomId = RoomId, UserId = AliceId, InstitutionId = InstitutionId },
            new RoomMembership { RoomId = RoomId, UserId = BobId, InstitutionId = InstitutionId });
        _db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), RoomId = RoomId, StartedAtUtc = DateTime.UtcNow });
        _db.SaveChanges();
        _messaging = new MessagingService(new MessagingRepository(_db), new TestClock());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_live_meeting_survives_one_leave_and_ends_when_the_last_member_leaves()
    {
        await _messaging.LeaveAsync(RoomId, new CommunicationCaller(AliceId, UserRole.Teacher, InstitutionId), CancellationToken.None);
        Assert.Null((await _db.Meetings.SingleAsync()).EndedAtUtc); // Bob is still there

        await _messaging.LeaveAsync(RoomId, new CommunicationCaller(BobId, UserRole.Teacher, InstitutionId), CancellationToken.None);
        Assert.NotNull((await _db.Meetings.SingleAsync()).EndedAtUtc);
    }
}
