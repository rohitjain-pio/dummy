using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Communication;

// CT-301 — API-037–040. 14-test-spec/communication.md TC-505, plus BRL-036/037/039 and the
// VAL-016/017 rules and Idempotency-Key behaviour the contract names.
public class AnnouncementsEndpointsTests : IAsyncLifetime
{
    private SchoolSyncApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private readonly Guid _teacherUserId = Guid.NewGuid();
    private readonly Guid _principalUserId = Guid.NewGuid();

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

    private record AnnouncementRow(Guid Id, string Title, string Content, DateOnly EffectiveDate, Guid AuthorId, DateTime CreatedAtUtc, bool IsWithdrawn, DateTime? WithdrawnAtUtc);
    private record ErrorBody(string Code, string Message);

    private static object Body(string title = "Sports Day", string content = "Friday on the main ground.", string effectiveDate = "2026-09-20") =>
        new { title, content, effectiveDate };

    private async Task<AnnouncementRow> CreateAsTeacherAsync(string title = "Sports Day")
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        var response = await _client.PostAsJsonAsync("/api/v1/announcements", Body(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AnnouncementRow>())!;
    }

    [Fact] // TC-505 — adversarial: fails against an implementation that lets any canPost role withdraw
    public async Task A_teacher_cannot_withdraw_even_their_own_announcement()
    {
        var created = await CreateAsTeacherAsync();

        var response = await _client.PostAsync($"/api/v1/announcements/{created.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ERR-501", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
        var list = (await _client.GetFromJsonAsync<List<AnnouncementRow>>("/api/v1/announcements"))!;
        Assert.Contains(list, a => a.Id == created.Id); // still published
    }

    [Fact] // BRL-038 positive: leadership withdraws; it leaves the list and the detail route
    public async Task Leadership_withdraws_an_announcement_and_it_is_no_longer_current()
    {
        var created = await CreateAsTeacherAsync();
        AuthenticateAs(_principalUserId, UserRole.Principal);

        var response = await _client.PostAsync($"/api/v1/announcements/{created.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AnnouncementRow>())!;
        Assert.True(body.IsWithdrawn);
        Assert.NotNull(body.WithdrawnAtUtc);
        Assert.DoesNotContain((await _client.GetFromJsonAsync<List<AnnouncementRow>>("/api/v1/announcements"))!, a => a.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/announcements/{created.Id}")).StatusCode);
    }

    [Fact] // BRL-039 — adversarial: amendment never overwrites author or creation date
    public async Task Amending_changes_content_but_never_the_author_or_creation_date()
    {
        var created = await CreateAsTeacherAsync();
        AuthenticateAs(_principalUserId, UserRole.Principal);

        var response = await _client.PutAsJsonAsync($"/api/v1/announcements/{created.Id}",
            Body("Sports Day (moved)", "Now on Saturday.", "2026-09-21"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var amended = (await response.Content.ReadFromJsonAsync<AnnouncementRow>())!;
        Assert.Equal("Now on Saturday.", amended.Content);
        Assert.Equal(new DateOnly(2026, 9, 21), amended.EffectiveDate);
        Assert.Equal(_teacherUserId, amended.AuthorId); // not the principal who amended it
        Assert.Equal(created.CreatedAtUtc, amended.CreatedAtUtc);
    }

    [Theory] // BRL-037: create/amend is leadership + Teacher only
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    public async Task Students_and_parents_can_read_but_not_post_or_amend(UserRole role)
    {
        var created = await CreateAsTeacherAsync();
        AuthenticateAs(Guid.NewGuid(), role);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/announcements", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync($"/api/v1/announcements/{created.Id}", Body())).StatusCode);
        Assert.Contains((await _client.GetFromJsonAsync<List<AnnouncementRow>>("/api/v1/announcements"))!, a => a.Id == created.Id);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/announcements/{created.Id}")).StatusCode);
    }

    [Fact] // BRL-036 — not merely hidden: another institution's users get nothing back at all
    public async Task Another_institution_cannot_see_list_or_detail()
    {
        var created = await CreateAsTeacherAsync();
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, otherInstitution);

        Assert.Empty((await _client.GetFromJsonAsync<List<AnnouncementRow>>("/api/v1/announcements"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/announcements/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/v1/announcements/{created.Id}/withdraw", null)).StatusCode);
    }

    [Theory] // VAL-016 / VAL-017 / §2.7
    [InlineData("", "content", "2026-09-20")]
    [InlineData("   ", "content", "2026-09-20")]
    [InlineData("title", "", "2026-09-20")]
    [InlineData("title", "content", null)]
    public async Task Invalid_announcements_are_rejected_with_ERR_901(string title, string content, string? effectiveDate)
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        var response = await _client.PostAsJsonAsync("/api/v1/announcements", new { title, content, effectiveDate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ERR-901", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Fact] // VAL-016 length bound at the column's 200, with its positive control
    public async Task Title_is_bounded_at_200_characters()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/announcements", Body(new string('a', 200)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/announcements", Body(new string('a', 201)))).StatusCode);
    }

    [Fact] // VAL-017: future-dated announcements are allowed
    public async Task A_future_effective_date_is_accepted()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/announcements", Body(effectiveDate: "2027-01-15"))).StatusCode);
    }

    [Fact] // Idempotency-Key: a retry returns the original 201 body, and the key is scoped per institution
    public async Task A_repeated_Idempotency_Key_replays_the_original_announcement_without_creating_a_second()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        var key = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> PostAsync(string title)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/announcements") { Content = JsonContent.Create(Body(title)) };
            request.Headers.Add("Idempotency-Key", key);
            return await _client.SendAsync(request);
        }

        var first = (await (await PostAsync("First")).Content.ReadFromJsonAsync<AnnouncementRow>())!;
        var retry = await PostAsync("Retried body is ignored");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(first.Id, (await retry.Content.ReadFromJsonAsync<AnnouncementRow>())!.Id);
        Assert.Single((await _client.GetFromJsonAsync<List<AnnouncementRow>>("/api/v1/announcements"))!);

        // The same client-chosen key from another institution must not replay this tenant's response.
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        AuthenticateAs(Guid.NewGuid(), UserRole.Teacher, otherInstitution);
        var foreign = (await (await PostAsync("Other school's post")).Content.ReadFromJsonAsync<AnnouncementRow>())!;
        Assert.NotEqual(first.Id, foreign.Id);
    }
}
