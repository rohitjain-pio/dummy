using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Classes;

// CT-105's gate is a contract test: "Contract test confirms Teacher role reaches GET /classes".
// Its DoD is BR-026/028/029 enforced. Both are covered here.
public class ClassesEndpointsTests : IAsyncLifetime
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

    private record ClassResponse(Guid Id, string Name, string Section, Guid? ClassTeacherId);
    private record ErrorBody(string Code, string Message);

    private async Task<Guid> CreateClassAsync(string name = "Grade 5", string section = "A")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/classes", new { name, section });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClassResponse>())!.Id;
    }

    private async Task PlaceStudentAsync(Guid classId, string sr = "SR-1", string roll = "ROLL-1")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/students", new
        {
            srNumber = sr,
            rollNumber = roll,
            name = "Asha Menon",
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd"),
            gender = "Female",
            email = "asha@example.edu",
            classId,
            guardianFatherName = (string?)null,
            guardianMotherName = (string?)null,
            guardianContactEmail = (string?)null,
            guardianPhone = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // CT-105's named gate — FR-027's correction to the mockup's nav exclusion
    public async Task A_teacher_can_read_the_class_list()
    {
        await CreateClassAsync();

        AuthenticateAs(UserRole.Teacher);
        var response = await _client.GetAsync("/api/v1/classes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classes = await response.Content.ReadFromJsonAsync<List<ClassResponse>>();
        Assert.Single(classes!);
    }

    [Fact] // The same grant applies to the single-class read
    public async Task A_teacher_can_read_a_single_class()
    {
        var classId = await CreateClassAsync();

        AuthenticateAs(UserRole.Teacher);
        var response = await _client.GetAsync($"/api/v1/classes/{classId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory] // BRL-029: "Teacher role: read-only, 403 on write" — every write, not just one
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task A_teacher_cannot_mutate_class_structure(string method)
    {
        var classId = await CreateClassAsync();
        AuthenticateAs(UserRole.Teacher);

        var response = method switch
        {
            "POST" => await _client.PostAsJsonAsync("/api/v1/classes", new { name = "Grade 6", section = "B" }),
            "PUT" => await _client.PutAsJsonAsync($"/api/v1/classes/{classId}", new { name = "Grade 6", section = "B" }),
            _ => await _client.DeleteAsync($"/api/v1/classes/{classId}")
        };

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] // BRL-026 -> ERR-305
    public async Task A_duplicate_name_and_section_within_the_institution_is_rejected()
    {
        await CreateClassAsync("Grade 5", "A");

        var response = await _client.PostAsJsonAsync("/api/v1/classes", new { name = "Grade 5", section = "A" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-305", error!.Code);
    }

    [Fact] // BRL-026 is the PAIR, not the name alone — the same name in a different section is fine
    public async Task The_same_class_name_in_a_different_section_is_not_a_duplicate()
    {
        await CreateClassAsync("Grade 5", "A");

        var response = await _client.PostAsJsonAsync("/api/v1/classes", new { name = "Grade 5", section = "B" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // BRL-026 is scoped to the institution — another school may use the same pair
    public async Task The_same_name_and_section_in_another_institution_is_not_a_duplicate()
    {
        await CreateClassAsync("Grade 5", "A");

        var otherInstitutionId = await _factory.SeedInstitutionAsync("Other School");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.IssueTokenFor(Guid.NewGuid(), UserRole.Administrator, otherInstitutionId));

        var response = await _client.PostAsJsonAsync("/api/v1/classes", new { name = "Grade 5", section = "A" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // BRL-028 -> ERR-306
    public async Task A_class_with_students_still_placed_cannot_be_deleted()
    {
        var classId = await CreateClassAsync();
        await PlaceStudentAsync(classId);

        var response = await _client.DeleteAsync($"/api/v1/classes/{classId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-306", error!.Code);

        // And it is genuinely still there — a guard that returned the error after removing the
        // row would satisfy the status code and destroy the history BRL-028 protects.
        AuthenticateAs(UserRole.Administrator);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/classes/{classId}")).StatusCode);
    }

    [Fact] // BRL-028 counts WITHDRAWN students too — their history must stay attributable
    public async Task A_class_whose_only_student_has_been_withdrawn_still_cannot_be_deleted()
    {
        var classId = await CreateClassAsync();
        await PlaceStudentAsync(classId);

        var students = await _client.GetFromJsonAsync<List<StudentIdOnly>>("/api/v1/students");
        var withdraw = await _client.PostAsync($"/api/v1/students/{students!.Single().Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        var response = await _client.DeleteAsync($"/api/v1/classes/{classId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact] // Positive control: an empty class deletes cleanly
    public async Task An_empty_class_can_be_deleted()
    {
        var classId = await CreateClassAsync();

        var response = await _client.DeleteAsync($"/api/v1/classes/{classId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/classes/{classId}")).StatusCode);
    }

    [Fact] // SPEC-FR-027: the class teacher is shown on the class read
    public async Task The_class_read_shows_the_assigned_class_teacher()
    {
        var classId = await CreateClassAsync();
        var teacher = await _client.PostAsJsonAsync("/api/v1/teachers", new
        {
            name = "Meera Rao",
            contactEmail = "meera@example.edu",
            telephone = "+919000000002",
            gender = "Female",
            address = "12 Staff Lane",
            joiningDate = "2024-06-01"
        });
        var teacherId = (await teacher.Content.ReadFromJsonAsync<TeacherIdOnly>())!.Id;

        await _client.PutAsJsonAsync($"/api/v1/classes/{classId}/class-teacher", new { teacherId });

        var body = await _client.GetFromJsonAsync<ClassResponse>($"/api/v1/classes/{classId}");
        Assert.Equal(teacherId, body!.ClassTeacherId);
    }

    [Fact] // API-029 + API-030, and the subject-deletion path that clears its own mappings
    public async Task A_subject_can_be_mapped_to_a_class_and_then_deleted()
    {
        var classId = await CreateClassAsync();
        var teacher = await _client.PostAsJsonAsync("/api/v1/teachers", new
        {
            name = "Meera Rao",
            contactEmail = "meera@example.edu",
            telephone = "+919000000002",
            gender = "Female",
            address = "12 Staff Lane",
            joiningDate = "2024-06-01"
        });
        var teacherId = (await teacher.Content.ReadFromJsonAsync<TeacherIdOnly>())!.Id;

        var created = await _client.PostAsJsonAsync("/api/v1/subjects", new { name = "Mathematics" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var subjectId = (await created.Content.ReadFromJsonAsync<SubjectIdOnly>())!.Id;

        var mapped = await _client.PostAsJsonAsync(
            $"/api/v1/subjects/{subjectId}/class-mappings", new { teacherId, classId });
        Assert.Equal(HttpStatusCode.Created, mapped.StatusCode);

        // Deleting a mapped subject must not fail on the Restrict FK — its join rows go with it.
        var deleted = await _client.DeleteAsync($"/api/v1/subjects/{subjectId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact] // BRL-029 covers the subject catalogue too
    public async Task A_teacher_cannot_create_a_subject()
    {
        AuthenticateAs(UserRole.Teacher);

        var response = await _client.PostAsJsonAsync("/api/v1/subjects", new { name = "Mathematics" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private record StudentIdOnly(Guid Id);
    private record TeacherIdOnly(Guid Id);
    private record SubjectIdOnly(Guid Id);
}
