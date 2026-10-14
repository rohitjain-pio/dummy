using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Communication;

// CT-303 — `/hubs/collaboration` through the real ASP.NET Core pipeline: real SignalR clients over
// WebSockets against the test server, authenticating with `access_token` on the hub URL exactly as
// a browser does. 14-test-spec/communication.md TC-503 and TC-506 (Layer: E2E — here the "end" is
// the SignalR client protocol; the browser UI is CT-603 per Q-218).
public class CollaborationHubTests : IAsyncLifetime
{
    private const string Passcode = "birch-4412";
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private SchoolSyncApiFactory _factory = null!;
    private Guid _institutionId;
    private Guid _roomId;
    private readonly Guid _aliceId = Guid.NewGuid(); // creator
    private readonly Guid _bobId = Guid.NewGuid(); // joins with the passcode
    private readonly List<HubConnection> _connections = [];

    public async Task InitializeAsync()
    {
        _factory = new SchoolSyncApiFactory();
        _ = _factory.CreateClient(); // starts the test server
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();

        var alice = RestAs(_aliceId, UserRole.Teacher);
        var created = await alice.PostAsJsonAsync("/api/v1/rooms", new { name = "Grade 5 Science", passcode = Passcode });
        _roomId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await RestAs(_bobId, UserRole.Student).PostAsJsonAsync($"/api/v1/rooms/{_roomId}/join", new { passcode = Passcode })).StatusCode);
    }

    public async Task DisposeAsync()
    {
        foreach (var c in _connections) await c.DisposeAsync();
        _factory.Dispose();
    }

    private HttpClient RestAs(Guid userId, UserRole role, Guid? institutionId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));
        return client;
    }

    private async Task<HubConnection> ConnectAsync(Guid userId, UserRole role, Guid? institutionId = null)
    {
        var token = _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId);
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/collaboration"), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                // A browser cannot set headers on a WebSocket, so it sends the JWT as ?access_token=.
                // The .NET client would use a header instead; putting the token on the URL here
                // exercises the browser path the server's OnMessageReceived hook exists for.
                options.WebSocketFactory = async (context, ct) =>
                {
                    var uri = new UriBuilder(context.Uri) { Query = $"access_token={Uri.EscapeDataString(token)}" }.Uri;
                    return await server.CreateWebSocketClient().ConnectAsync(uri, ct);
                };
            })
            .Build();
        _connections.Add(connection);
        await connection.StartAsync();
        return connection;
    }

    // Collects one event type so a test can await "the next one matching".
    private sealed class Inbox
    {
        private readonly ConcurrentQueue<JsonElement> _received = new();
        private readonly SemaphoreSlim _signal = new(0);

        public Inbox(HubConnection connection, string eventName) =>
            connection.On<JsonElement>(eventName, payload => { _received.Enqueue(payload); _signal.Release(); });

        public async Task<JsonElement> NextAsync(Func<JsonElement, bool>? match = null)
        {
            var deadline = DateTime.UtcNow + EventTimeout;
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !await _signal.WaitAsync(remaining))
                    throw new TimeoutException("Expected hub event was not received.");
                if (_received.TryDequeue(out var item) && (match is null || match(item))) return item;
            }
        }

        public async Task<bool> NoneWithinAsync(TimeSpan window) => !await _signal.WaitAsync(window);
    }

    [Fact] // TC-503 — Client B receives Client A's message via EVT-001 within 1s (NFR-008)
    public async Task A_message_sent_by_one_member_reaches_another_member_within_one_second()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        var bob = await ConnectAsync(_bobId, UserRole.Student);
        var bobInbox = new Inbox(bob, "MessageSent");
        await alice.InvokeAsync("JoinRoom", _roomId);
        await bob.InvokeAsync("JoinRoom", _roomId);

        var stopwatch = Stopwatch.StartNew();
        await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Lab reports due Friday");
        var received = await bobInbox.NextAsync();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"EVT-001 took {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal("Lab reports due Friday", received.GetProperty("content").GetString());
        Assert.Equal(_aliceId, received.GetProperty("authorId").GetGuid()); // attributable (BRL-043)
    }

    [Fact] // TC-506 — disconnect, miss a message, reconnect: EVT-006 prompts a re-fetch that shows no gap
    public async Task After_reconnecting_the_client_is_told_to_resync_and_history_has_no_gap()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        await alice.InvokeAsync("JoinRoom", _roomId);
        var bob = await ConnectAsync(_bobId, UserRole.Student);
        await bob.InvokeAsync("JoinRoom", _roomId);

        await bob.StopAsync(); // Bob drops off (a disconnect is presence only — Q-219)
        await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Sent while Bob was away");

        var bobAgain = await ConnectAsync(_bobId, UserRole.Student); // no passcode needed to come back
        var resync = new Inbox(bobAgain, "HistoryResync");
        await bobAgain.InvokeAsync("JoinRoom", _roomId);
        Assert.Equal(_roomId, (await resync.NextAsync()).GetProperty("roomId").GetGuid());

        var history = await RestAs(_bobId, UserRole.Student).GetFromJsonAsync<List<JsonElement>>($"/api/v1/rooms/{_roomId}/messages");
        Assert.Contains(history!, m => m.GetProperty("content").GetString() == "Sent while Bob was away");
    }

    [Fact] // Hub authorization: a non-member cannot join the group, and so receives nothing
    public async Task A_user_who_never_supplied_the_passcode_cannot_join_the_room_group()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        await alice.InvokeAsync("JoinRoom", _roomId);
        var eve = await ConnectAsync(Guid.NewGuid(), UserRole.Student); // same institution, no passcode
        var eveInbox = new Inbox(eve, "MessageSent");

        var refused = await Assert.ThrowsAsync<HubException>(() => eve.InvokeAsync("JoinRoom", _roomId));
        Assert.Contains("ERR-102", refused.Message);

        await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Members only");
        Assert.True(await eveInbox.NoneWithinAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact] // Another institution's user is refused even though the room id is known
    public async Task A_user_from_another_institution_is_refused()
    {
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        var mallory = await ConnectAsync(_aliceId, UserRole.Teacher, otherInstitution); // same user id, other tenant

        await Assert.ThrowsAsync<HubException>(() => mallory.InvokeAsync("JoinRoom", _roomId));
    }

    [Fact] // The ?access_token= query path is honoured for /hubs only — never for REST endpoints
    public async Task A_token_in_the_query_string_does_not_authenticate_a_REST_call()
    {
        var anonymous = _factory.CreateClient();
        var token = Uri.EscapeDataString(_factory.IssueTokenFor(_aliceId, UserRole.Teacher, _institutionId));

        var response = await anonymous.GetAsync($"/api/v1/rooms?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] // A member must JoinRoom on this connection before sending; ERR-504 over the hub
    public async Task Sending_requires_joining_on_this_connection_and_respects_the_length_bound()
    {
        var bob = await ConnectAsync(_bobId, UserRole.Student);

        await Assert.ThrowsAsync<HubException>(() => bob.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Too early"));

        await bob.InvokeAsync("JoinRoom", _roomId);
        var tooLong = await Assert.ThrowsAsync<HubException>(() => bob.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), new string('x', 4001)));
        Assert.Contains("ERR-504", tooLong.Message);
    }

    [Fact] // EVT-001 idempotency over the wire: a retried send is acknowledged but not re-broadcast
    public async Task A_retried_send_is_acknowledged_as_a_duplicate_and_delivered_only_once()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        var bob = await ConnectAsync(_bobId, UserRole.Student);
        var bobInbox = new Inbox(bob, "MessageSent");
        await alice.InvokeAsync("JoinRoom", _roomId);
        await bob.InvokeAsync("JoinRoom", _roomId);
        var clientId = Guid.NewGuid();

        var first = await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, clientId, "Once");
        var retry = await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, clientId, "Once");

        Assert.Equal(first.GetProperty("messageId").GetGuid(), retry.GetProperty("messageId").GetGuid());
        Assert.True(retry.GetProperty("wasDuplicate").GetBoolean());
        await bobInbox.NextAsync();
        Assert.True(await bobInbox.NoneWithinAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact] // EVT-002 / EVT-003 / EVT-004: presence, typing, and join/leave notices
    public async Task Presence_typing_and_join_leave_events_reach_the_other_members()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        var presence = new Inbox(alice, "PresenceChanged");
        var typing = new Inbox(alice, "TypingChanged");
        var joined = new Inbox(alice, "ParticipantJoined");
        var left = new Inbox(alice, "ParticipantLeft");
        await alice.InvokeAsync("JoinRoom", _roomId);

        // EVT-004 "joined" fires when membership is created (REST API-043), per Q-219.
        var carolId = Guid.NewGuid();
        await RestAs(carolId, UserRole.Student).PostAsJsonAsync($"/api/v1/rooms/{_roomId}/join", new { passcode = Passcode });
        Assert.Equal(carolId, (await joined.NextAsync()).GetProperty("userId").GetGuid());

        var carol = await ConnectAsync(carolId, UserRole.Student);
        await carol.InvokeAsync("JoinRoom", _roomId);
        var online = await presence.NextAsync(p => p.GetProperty("userId").GetGuid() == carolId);
        Assert.Equal("online", online.GetProperty("status").GetString());

        await carol.InvokeAsync("Typing", _roomId, true);
        Assert.True((await typing.NextAsync()).GetProperty("isTyping").GetBoolean());

        await carol.InvokeAsync("LeaveRoom", _roomId);
        Assert.Equal("left", (await left.NextAsync()).GetProperty("event").GetString());
        Assert.Equal("offline", (await presence.NextAsync(p => p.GetProperty("userId").GetGuid() == carolId)).GetProperty("status").GetString());

        // Q-219: leaving ended membership, so coming back needs the passcode again.
        await Assert.ThrowsAsync<HubException>(() => carol.InvokeAsync("JoinRoom", _roomId));
    }

    [Fact] // Q-219: a dropped connection is offline presence, not a leave
    public async Task Disconnecting_shows_offline_but_does_not_announce_a_leave()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        var presence = new Inbox(alice, "PresenceChanged");
        var left = new Inbox(alice, "ParticipantLeft");
        await alice.InvokeAsync("JoinRoom", _roomId);
        var bob = await ConnectAsync(_bobId, UserRole.Student);
        await bob.InvokeAsync("JoinRoom", _roomId);
        await presence.NextAsync(p => p.GetProperty("status").GetString() == "online");

        await bob.StopAsync();

        Assert.Equal("offline", (await presence.NextAsync(p => p.GetProperty("userId").GetGuid() == _bobId)).GetProperty("status").GetString());
        Assert.True(await left.NoneWithinAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact] // Removing the room takes effect on live connections at the next call, not at reconnect
    public async Task Once_the_room_is_removed_a_connected_member_can_no_longer_send()
    {
        var bob = await ConnectAsync(_bobId, UserRole.Student);
        await bob.InvokeAsync("JoinRoom", _roomId);
        Assert.Equal(HttpStatusCode.NoContent, (await RestAs(_aliceId, UserRole.Teacher).DeleteAsync($"/api/v1/rooms/{_roomId}")).StatusCode);

        await Assert.ThrowsAsync<HubException>(() => bob.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Anyone?"));
    }

    [Fact] // API-046 over REST: author-only revise/withdraw; history reflects both
    public async Task The_author_revises_and_withdraws_over_REST_and_others_are_refused()
    {
        var alice = await ConnectAsync(_aliceId, UserRole.Teacher);
        await alice.InvokeAsync("JoinRoom", _roomId);
        var ack = await alice.InvokeAsync<JsonElement>("SendMessage", _roomId, Guid.NewGuid(), "Quiz on Monday");
        var messageId = ack.GetProperty("messageId").GetGuid();

        var bobRest = RestAs(_bobId, UserRole.Student);
        var notAuthor = await bobRest.PutAsJsonAsync($"/api/v1/rooms/{_roomId}/messages/{messageId}", new { content = "No quiz!" });
        Assert.Equal(HttpStatusCode.Forbidden, notAuthor.StatusCode);
        Assert.Contains("ERR-102", await notAuthor.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await bobRest.DeleteAsync($"/api/v1/rooms/{_roomId}/messages/{messageId}")).StatusCode);

        var aliceRest = RestAs(_aliceId, UserRole.Teacher);
        Assert.Equal(HttpStatusCode.BadRequest, (await aliceRest.PutAsJsonAsync($"/api/v1/rooms/{_roomId}/messages/{messageId}", new { content = new string('x', 4001) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await aliceRest.PutAsJsonAsync($"/api/v1/rooms/{_roomId}/messages/{messageId}", new { content = "Quiz on Tuesday" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await aliceRest.DeleteAsync($"/api/v1/rooms/{_roomId}/messages/{messageId}")).StatusCode);

        var history = await bobRest.GetFromJsonAsync<List<JsonElement>>($"/api/v1/rooms/{_roomId}/messages");
        var row = Assert.Single(history!);
        Assert.True(row.GetProperty("isWithdrawn").GetBoolean());
        Assert.Equal(string.Empty, row.GetProperty("content").GetString());
    }
}
