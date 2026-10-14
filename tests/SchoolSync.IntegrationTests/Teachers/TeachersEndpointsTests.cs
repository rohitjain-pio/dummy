using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Teachers;

// 14-test-spec/academic-records.md TC-303 and TC-304, both Layer: Integration, plus the rest of
// BRL-022–025 that CT-104's Definition of Done calls out.
public class TeachersEndpointsTests : IAsyncLifetime
{
    private SchoolSyncApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;

    public async Task InitializeAsync()
    {
        _factory = new SchoolSyncApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        AuthenticateAs(UserRole.Administrator);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(UserRole role) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(Guid.NewGuid(), role, _institutionId));

    private static object TeacherBody(string name = "Meera Rao", string email = "meera.rao@example.edu") => new
    {
        name,
        contactEmail = email,
        telephone = "+919000000002",
        gender = "Female",
        address = "12 Staff Lane",
        joiningDate = "2024-06-01"
    };

    private record TeacherResponse(Guid Id, string Name, string ContactEmail, bool IsActive, DateTime? DeactivatedAtUtc);
    private record ErrorBody(string Code, string Message);

    private async Task<Guid> CreateTeacherAsync(string name = "Meera Rao", string email = "meera.rao@example.edu")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/teachers", TeacherBody(name, email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TeacherResponse>())!.Id;
    }

    private async Task AssignClassTeacherAsync(Guid classId, Guid teacherId, HttpStatusCode expected)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/classes/{classId}/class-teacher", new { teacherId });
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact] // TC-303 — adversarial: fails against an implementation allowing multiple class-teacher holdings
    public async Task A_teacher_already_class_teacher_of_class_A_cannot_also_take_class_B()
    {
        var teacherId = await CreateTeacherAsync();
        var classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        var classB = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");

        await AssignClassTeacherAsync(classA, teacherId, HttpStatusCode.OK);

        // Class B is EMPTY, so the only thing that can reject this is the teacher-side clause of
        // BRL-023. An implementation that only checked whether the target class was occupied
        // would accept this and leave the teacher running two classes.
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/classes/{classB}/class-teacher", new { teacherId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-303", error!.Code);
    }

    [Fact] // TC-304 — adversarial: "ERR-304, teacher NOT deactivated"
    public async Task Removing_a_teacher_who_still_holds_a_class_is_rejected_and_leaves_them_active()
    {
        var teacherId = await CreateTeacherAsync();
        var classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        await AssignClassTeacherAsync(classA, teacherId, HttpStatusCode.OK);

        var response = await _client.PostAsync($"/api/v1/teachers/{teacherId}/remove", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-304", error!.Code);

        // The half that actually matters: an implementation that deactivated first and reported
        // the conflict afterwards would satisfy the status code while orphaning the assignment
        // BR-025 exists to protect.
        var after = await _client.GetFromJsonAsync<TeacherResponse>($"/api/v1/teachers/{teacherId}");
        Assert.True(after!.IsActive);
        Assert.Null(after.DeactivatedAtUtc);
    }

    [Fact] // BRL-025's other holding: a subject assignment blocks removal just as a class does
    public async Task Removing_a_teacher_who_holds_only_a_subject_assignment_is_also_rejected()
    {
        var teacherId = await CreateTeacherAsync();
        var classId = await _factory.SeedClassAsync(_institutionId);
        var subjectId = await _factory.SeedSubjectAsync(_institutionId);

        var assign = await _client.PostAsJsonAsync(
            $"/api/v1/subjects/{subjectId}/teacher-assignments", new { teacherId, classId });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);

        var response = await _client.PostAsync($"/api/v1/teachers/{teacherId}/remove", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-304", error!.Code);
    }

    [Fact] // Positive control: with nothing held, removal succeeds — so the two tests above
           // are not passing merely because removal is broken outright
    public async Task A_teacher_holding_nothing_can_be_removed()
    {
        var teacherId = await CreateTeacherAsync();

        var response = await _client.PostAsync($"/api/v1/teachers/{teacherId}/remove", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TeacherResponse>();
        Assert.False(body!.IsActive);
    }

    [Fact] // BRL-025: "until the first is released" — releasing must actually unblock removal
    public async Task Releasing_the_class_teacher_assignment_unblocks_removal()
    {
        var teacherId = await CreateTeacherAsync();
        var classA = await _factory.SeedClassAsync(_institutionId);
        await AssignClassTeacherAsync(classA, teacherId, HttpStatusCode.OK);

        var blocked = await _client.PostAsync($"/api/v1/teachers/{teacherId}/remove", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // Release: a null teacherId, the only release path API-024 offers.
        var release = await _client.PutAsJsonAsync(
            $"/api/v1/classes/{classA}/class-teacher", new { teacherId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        var afterRelease = await _client.PostAsync($"/api/v1/teachers/{teacherId}/remove", null);
        Assert.Equal(HttpStatusCode.OK, afterRelease.StatusCode);
    }

    [Fact] // BRL-023 clause 1: the class itself is already occupied, by a DIFFERENT teacher
    public async Task Assigning_a_second_teacher_to_an_already_occupied_class_is_rejected()
    {
        var first = await CreateTeacherAsync("Meera Rao", "meera@example.edu");
        var second = await CreateTeacherAsync("Nikhil Shah", "nikhil@example.edu");
        var classA = await _factory.SeedClassAsync(_institutionId);

        await AssignClassTeacherAsync(classA, first, HttpStatusCode.OK);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/classes/{classA}/class-teacher", new { teacherId = second });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-303", error!.Code);
    }

    [Fact] // Re-asserting the SAME holding is idempotent, not "a second assignment"
    public async Task Reassigning_the_same_teacher_to_the_same_class_succeeds()
    {
        var teacherId = await CreateTeacherAsync();
        var classA = await _factory.SeedClassAsync(_institutionId);

        await AssignClassTeacherAsync(classA, teacherId, HttpStatusCode.OK);
        await AssignClassTeacherAsync(classA, teacherId, HttpStatusCode.OK);
    }

    [Fact] // BRL-024: a teacher MAY hold multiple (subject, class) pairs — the deliberate
           // contrast with BRL-023, and a place an over-zealous guard would wrongly reject
    public async Task A_teacher_may_hold_several_subject_and_class_assignments()
    {
        var teacherId = await CreateTeacherAsync();
        var classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        var classB = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        var maths = await _factory.SeedSubjectAsync(_institutionId, "Mathematics");
        var science = await _factory.SeedSubjectAsync(_institutionId, "Science");

        foreach (var (subjectId, classId) in new[] { (maths, classA), (maths, classB), (science, classA) })
        {
            var response = await _client.PostAsJsonAsync(
                $"/api/v1/subjects/{subjectId}/teacher-assignments", new { teacherId, classId });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact] // API-023 is leadership-only — explicitly NOT a peer directory for Teachers
    public async Task A_teacher_cannot_read_the_teacher_directory()
    {
        AuthenticateAs(UserRole.Teacher);

        var response = await _client.GetAsync("/api/v1/teachers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] // company.md §3's input-validation floor, where the spec declares no VAL- for teachers
    public async Task A_malformed_contact_email_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/teachers", TeacherBody(email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-901", error!.Code);
    }

    [Fact] // BRL-022 + the tenant filter: a teacher of another institution is not reachable
    public async Task A_teacher_of_another_institution_is_not_readable()
    {
        var teacherId = await CreateTeacherAsync();

        var otherInstitutionId = await _factory.SeedInstitutionAsync("Other School");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.IssueTokenFor(Guid.NewGuid(), UserRole.Administrator, otherInstitutionId));

        var response = await _client.GetAsync($"/api/v1/teachers/{teacherId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
