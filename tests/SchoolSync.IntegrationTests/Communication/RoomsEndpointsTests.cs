using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Communication;

// CT-302 — API-041–045. 14-test-spec/communication.md TC-501, TC-502, plus BRL-042/E2, the Q-219
// membership rules, and the passcode-hashing DoD ("passcode never in any response after creation").
public class RoomsEndpointsTests : IAsyncLifetime
{
    private const string Passcode = "maple-7731";

    private SchoolSyncApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private readonly Guid _creatorUserId = Guid.NewGuid();
    private readonly Guid _joinerUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory = new SchoolSyncApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private record RoomRow(Guid Id, string Name, string? Description, Guid CreatedByUserId);
    private record MessageRow(Guid Id, Guid AuthorId, string Content, bool IsWithdrawn);
    private record ErrorBody(string Code, string Message);

    private async Task<(Guid Id, string RawCreateResponse)> CreateRoomAsync(string name = "Grade 5 Science")
    {
        AuthenticateAs(_creatorUserId, UserRole.Teacher);
        var response = await _client.PostAsJsonAsync("/api/v1/rooms", new { name, description = "Weekly project room", passcode = Passcode });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        return ((await response.Content.ReadFromJsonAsync<RoomRow>())!.Id, raw);
    }

    private async Task<T> WithDbAsync<T>(Func<SchoolSyncDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>());
    }

    private Task<HttpResponseMessage> JoinAsync(Guid roomId, string passcode) =>
        _client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/join", new { passcode });

    [Fact] // TC-501 — adversarial: fails if the DTO leaks the hash or the plaintext
    public async Task Neither_the_room_list_nor_the_create_response_contains_any_passcode_field()
    {
        var (roomId, rawCreate) = await CreateRoomAsync();
        var storedHash = await WithDbAsync(db => db.Rooms.IgnoreQueryFilters().Where(r => r.Id == roomId).Select(r => r.PasscodeHash).SingleAsync());
        AuthenticateAs(_joinerUserId, UserRole.Student);

        var rawList = await (await _client.GetAsync("/api/v1/rooms")).Content.ReadAsStringAsync();

        foreach (var raw in new[] { rawCreate, rawList })
        {
            Assert.DoesNotContain("passcode", raw, StringComparison.OrdinalIgnoreCase); // no field of that name, hashed or not
            Assert.DoesNotContain(Passcode, raw);
            Assert.DoesNotContain(storedHash, raw);
        }
        Assert.Contains(roomId.ToString(), rawList);
    }

    [Fact] // VAL-019: stored hashed, never as the plaintext
    public async Task The_passcode_is_stored_as_a_verifiable_hash_not_plaintext()
    {
        var (roomId, _) = await CreateRoomAsync();

        var hash = await WithDbAsync(db => db.Rooms.IgnoreQueryFilters().Where(r => r.Id == roomId).Select(r => r.PasscodeHash).SingleAsync());

        Assert.NotEqual(Passcode, hash);
        Assert.DoesNotContain(Passcode, hash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Passcode, hash));
    }

    [Fact] // TC-502 — adversarial: wrong passcode -> ERR-502, no membership, no history
    public async Task A_wrong_passcode_is_rejected_with_no_membership_and_no_history()
    {
        var (roomId, _) = await CreateRoomAsync();
        AuthenticateAs(_joinerUserId, UserRole.Student);

        var join = await JoinAsync(roomId, "maple-7732"); // one character off

        Assert.Equal(HttpStatusCode.Forbidden, join.StatusCode);
        var error = await join.Content.ReadAsStringAsync();
        Assert.Contains("ERR-502", error);
        Assert.DoesNotContain("maple", error); // no passcode value echoed back (BRL-041)
        Assert.False(await WithDbAsync(db => db.RoomMemberships.IgnoreQueryFilters().AnyAsync(m => m.RoomId == roomId && m.UserId == _joinerUserId)));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/v1/rooms/{roomId}/messages")).StatusCode);
    }

    [Fact] // TC-502 positive control + Q-219: a correct passcode creates membership and opens history
    public async Task The_correct_passcode_joins_and_the_member_can_read_history_with_withdrawn_content_blanked()
    {
        var (roomId, _) = await CreateRoomAsync();
        await WithDbAsync(async db =>
        {
            db.Messages.AddRange(
                new Message { Id = Guid.NewGuid(), RoomId = roomId, AuthorId = _creatorUserId, Content = "Bring your notebooks", SentAtUtc = DateTime.UtcNow.AddMinutes(-2) },
                new Message { Id = Guid.NewGuid(), RoomId = roomId, AuthorId = _creatorUserId, Content = "Oops, wrong room", SentAtUtc = DateTime.UtcNow.AddMinutes(-1), IsWithdrawn = true });
            return await db.SaveChangesAsync();
        });
        AuthenticateAs(_joinerUserId, UserRole.Student);

        var join = await JoinAsync(roomId, Passcode);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Contains("/hubs/collaboration", await join.Content.ReadAsStringAsync());

        var history = (await _client.GetFromJsonAsync<List<MessageRow>>($"/api/v1/rooms/{roomId}/messages"))!;
        Assert.Equal(2, history.Count);
        Assert.Equal("Bring your notebooks", history[0].Content);
        Assert.True(history[1].IsWithdrawn);
        Assert.Equal(string.Empty, history[1].Content); // E3: kept in place, content blanked
        Assert.Equal(_creatorUserId, history[1].AuthorId); // author still shown

        // Joining again re-verifies the passcode and does not duplicate membership.
        Assert.Equal(HttpStatusCode.OK, (await JoinAsync(roomId, Passcode)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await JoinAsync(roomId, "wrong")).StatusCode);
        Assert.Equal(2, await WithDbAsync(db => db.RoomMemberships.IgnoreQueryFilters().CountAsync(m => m.RoomId == roomId)));
    }

    [Fact] // The creator is the room's first member without "joining" their own room
    public async Task The_creator_can_read_history_immediately()
    {
        var (roomId, _) = await CreateRoomAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/rooms/{roomId}/messages")).StatusCode);
    }

    [Fact] // BRL-042 — adversarial: a member who is neither creator nor leadership cannot remove the room
    public async Task A_non_creator_non_leadership_member_cannot_remove_the_room()
    {
        var (roomId, _) = await CreateRoomAsync();
        AuthenticateAs(_joinerUserId, UserRole.Teacher); // a Teacher, but not the creator
        await JoinAsync(roomId, Passcode);

        var response = await _client.DeleteAsync($"/api/v1/rooms/{roomId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ERR-503", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
        Assert.Contains((await _client.GetFromJsonAsync<List<RoomRow>>("/api/v1/rooms"))!, r => r.Id == roomId);
    }

    [Theory] // BRL-042 positive: creator, and leadership (E2 — works even if the creator is gone)
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_creator_or_leadership_removes_the_room_which_is_soft_deleted_and_closed_to_members(bool asCreator)
    {
        var (roomId, _) = await CreateRoomAsync();
        AuthenticateAs(_joinerUserId, UserRole.Student);
        await JoinAsync(roomId, Passcode);
        if (asCreator) AuthenticateAs(_creatorUserId, UserRole.Teacher); else AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/rooms/{roomId}")).StatusCode);

        Assert.DoesNotContain((await _client.GetFromJsonAsync<List<RoomRow>>("/api/v1/rooms"))!, r => r.Id == roomId);
        var row = await WithDbAsync(db => db.Rooms.IgnoreQueryFilters().SingleAsync(r => r.Id == roomId));
        Assert.False(row.IsActive); // D8: deactivated, not deleted
        Assert.Equal(0, await WithDbAsync(db => db.RoomMemberships.IgnoreQueryFilters().CountAsync(m => m.RoomId == roomId)));

        AuthenticateAs(_joinerUserId, UserRole.Student); // the former member
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/rooms/{roomId}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await JoinAsync(roomId, Passcode)).StatusCode);
    }

    [Fact] // BRL-040 / tenant isolation: another institution's rooms are invisible and unjoinable
    public async Task Another_institution_cannot_see_or_join_the_room_even_with_the_passcode()
    {
        var (roomId, _) = await CreateRoomAsync();
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, otherInstitution);

        Assert.Empty((await _client.GetFromJsonAsync<List<RoomRow>>("/api/v1/rooms"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await JoinAsync(roomId, Passcode)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/rooms/{roomId}")).StatusCode);
    }

    [Fact] // BRL-040: unauthenticated callers get 401 everywhere
    public async Task Anonymous_callers_are_rejected()
    {
        var (roomId, _) = await CreateRoomAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await JoinAsync(roomId, Passcode)).StatusCode);
    }

    [Theory] // VAL-018 / VAL-019
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("Room", "")]
    [InlineData("Room", null)]
    public async Task Rooms_need_a_name_and_a_passcode(string name, string? passcode)
    {
        AuthenticateAs(_creatorUserId, UserRole.Teacher);
        var response = await _client.PostAsJsonAsync("/api/v1/rooms", new { name, passcode });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ERR-901", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }
}
