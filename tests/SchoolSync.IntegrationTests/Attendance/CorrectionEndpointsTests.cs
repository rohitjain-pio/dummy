using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Attendance;

// CT-202 — API-033/034 through the real HTTP pipeline. 14-test-spec/attendance.md TC-407 and TC-412
// (both Integration), plus the status codes and role gates the state-machine unit tests cannot see.
public class CorrectionEndpointsTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private Guid _classId;
    private Guid _teacherUserId;
    private static readonly DateOnly Today = new(2026, 9, 14);

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classId = await _factory.SeedClassAsync(_institutionId);
        var (teacherId, teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        _teacherUserId = teacherUserId;
        await _factory.MakeClassTeacherAsync(_classId, teacherId);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, _institutionId));

    private record CorrectionBody(Guid Id, Guid AttendanceRecordId, string RequestedOutcome, string Status, string RecordOutcome, string? ResolutionNote);
    private record ErrorBody(string Code, string Message);

    private async Task<Guid> SeedRecordAsync(int daysAgo, Guid recordedBy)
    {
        var studentId = await _factory.SeedStudentAsync(_institutionId, _classId, $"SR-{Guid.NewGuid():N}", $"R-{Guid.NewGuid():N}");
        await _factory.SeedRecordAsync(_institutionId, studentId, Today.AddDays(-daysAgo), AttendanceOutcome.Absent, recordedBy);
        return await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().Where(r => r.PersonId == studentId).Select(r => r.Id).SingleAsync());
    }

    private Task<HttpResponseMessage> RequestCorrectionAsync(Guid recordId, string outcome = "Present") =>
        _client.PostAsJsonAsync($"/api/v1/attendance/{recordId}/correction-requests", new { requestedOutcome = outcome, reason = "Marked wrongly" });

    [Fact] // TC-407
    public async Task A_principal_approves_a_pending_request_and_the_record_updates()
    {
        var recordId = await SeedRecordAsync(daysAgo: 5, _teacherUserId);
        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        var created = await RequestCorrectionAsync(recordId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pending = (await created.Content.ReadFromJsonAsync<CorrectionBody>())!;
        Assert.Equal("Pending", pending.Status);
        Assert.Equal("Absent", pending.RecordOutcome);

        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var approved = await _client.PostAsync($"/api/v1/attendance/correction-requests/{pending.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = (await approved.Content.ReadFromJsonAsync<CorrectionBody>())!;
        Assert.Equal("Approved", body.Status);
        Assert.Equal("Present", body.RecordOutcome);

        var stored = await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().SingleAsync(r => r.Id == recordId));
        Assert.Equal(AttendanceOutcome.Present, stored.Outcome);
        Assert.Equal(CorrectionStatus.None, stored.CorrectionStatus);
    }

    [Fact] // TC-412 — leadership's own day-5 correction applies immediately, no self-approval loop (Q-205)
    public async Task Leadership_correcting_their_own_day_5_entry_applies_immediately()
    {
        var principalUserId = Guid.NewGuid();
        var recordId = await SeedRecordAsync(daysAgo: 5, principalUserId);
        AuthenticateAs(principalUserId, UserRole.Principal);

        var response = await RequestCorrectionAsync(recordId, "Leave");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<CorrectionBody>())!;
        Assert.Equal("AppliedDirectly", body.Status);
        Assert.Equal("Leave", body.RecordOutcome);
        Assert.Equal(0, await _factory.QueryAsync(db => db.CorrectionRequests.IgnoreQueryFilters()
            .CountAsync(r => r.Status == CorrectionRequestStatus.Pending)));
    }

    [Fact] // API-033 ERR-401 wire format: 423 with the catalogue body
    public async Task A_correction_on_a_31_day_old_record_returns_423_ERR_401()
    {
        var recordId = await SeedRecordAsync(daysAgo: 31, _teacherUserId);
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        var response = await RequestCorrectionAsync(recordId);

        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        Assert.Equal("ERR-401", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Fact] // API-034 role gate: approve/reject are leadership only (Q-204)
    public async Task A_teacher_cannot_call_approve_or_reject()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync($"/api/v1/attendance/correction-requests/{Guid.NewGuid()}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/v1/attendance/correction-requests/{Guid.NewGuid()}/reject", new { reason = "x" })).StatusCode);
    }

    [Fact] // Another institution's record is invisible to API-033 (BRL-032)
    public async Task A_correction_request_on_another_institutions_record_is_not_found()
    {
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        var otherClass = await _factory.SeedClassAsync(otherInstitution);
        var foreignStudent = await _factory.SeedStudentAsync(otherInstitution, otherClass, "SR-F", "R-F");
        await _factory.SeedRecordAsync(otherInstitution, foreignStudent, Today.AddDays(-2), AttendanceOutcome.Absent, Guid.NewGuid());
        var foreignRecordId = await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().Where(r => r.PersonId == foreignStudent).Select(r => r.Id).SingleAsync());
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var response = await RequestCorrectionAsync(foreignRecordId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
