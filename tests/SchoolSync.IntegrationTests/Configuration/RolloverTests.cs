using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Configuration;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Configuration;
using SchoolSync.IntegrationTests.Attendance;
using Xunit;

namespace SchoolSync.IntegrationTests.Configuration;

// CT-502 — API-059/060, PF-012. Gate: TC-701–703. DoD: append-only Enrollment proven (no historical mutation).
public class RolloverTests : IAsyncLifetime
{
    // Runs the rollover job like a Hangfire worker would: no HttpContext (flow suppressed), so the
    // job's ADR-0007 tenant scope is what scopes every query.
    private sealed class SynchronousRolloverQueue(IServiceScopeFactory scopes) : IRolloverQueue
    {
        public Task EnqueueAsync(Guid academicYearId, CancellationToken ct)
        {
            Task work;
            using (ExecutionContext.SuppressFlow())
                work = Task.Run(async () =>
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<RolloverJobRunner>().RunAsync(academicYearId, CancellationToken.None);
                });
            return work;
        }
    }

    private sealed class RolloverFactory : AttendanceApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<IRolloverQueue>();
                s.AddSingleton<IRolloverQueue>(sp => new SynchronousRolloverQueue(sp.GetRequiredService<IServiceScopeFactory>()));
            });
        }
    }

    private RolloverFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId, _grade5, _grade6, _grade12, _yearId;
    private Guid _asha, _ravi, _meena, _withdrawn; // asha/ravi in Grade 5, meena in Grade 12 (final year)
    private Guid _ashaPriorEnrollmentId;

    public async Task InitializeAsync()
    {
        _factory = new RolloverFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _grade5 = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        _grade6 = await _factory.SeedClassAsync(_institutionId, "Grade 6", "A");
        _grade12 = await _factory.SeedClassAsync(_institutionId, "Grade 12", "A");
        _asha = await _factory.SeedStudentAsync(_institutionId, _grade5, "SR-ASHA", "R-1");
        _ravi = await _factory.SeedStudentAsync(_institutionId, _grade5, "SR-RAVI", "R-2");
        _meena = await _factory.SeedStudentAsync(_institutionId, _grade12, "SR-MEENA", "R-3");
        _withdrawn = await _factory.SeedStudentAsync(_institutionId, _grade5, "SR-GONE", "R-4");
        AsPrincipal();

        await _client.PutAsJsonAsync($"/api/v1/classes/{_grade5}/next-class", new { nextClassId = _grade6 });
        var year = await (await _client.PostAsJsonAsync("/api/v1/academic-years", new { label = "2026-27", startDate = "2026-06-01", endDate = "2027-03-31" })).Content.ReadFromJsonAsync<JsonElement>();
        _yearId = year.GetProperty("id").GetGuid();

        _ashaPriorEnrollmentId = Guid.NewGuid();
        await _factory.QueryAsync(async db =>
        {
            (await db.Students.IgnoreQueryFilters().SingleAsync(s => s.Id == _withdrawn)).IsActive = false;
            db.Enrollments.Add(new Enrollment { Id = _ashaPriorEnrollmentId, AcademicYearId = _yearId, StudentId = _asha, ClassId = _grade5, CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) });
            return await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AsPrincipal() => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(Guid.NewGuid(), UserRole.Principal, _institutionId));

    private Task<List<Enrollment>> EnrollmentsAsync() => _factory.QueryAsync(db => db.Enrollments.AsNoTracking().ToListAsync());
    private Task<Student> StudentAsync(Guid id) => _factory.QueryAsync(db => db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == id));
    private Task<HttpResponseMessage> RolloverAsync() => _client.PostAsync($"/api/v1/academic-years/{_yearId}/rollover", null);
    private Task<HttpResponseMessage> HoldBackAsync(params Guid[] ids) => _client.PostAsJsonAsync($"/api/v1/academic-years/{_yearId}/rollover/hold-back", new { studentIds = ids });

    [Fact] // TC-701 — adversarial: new rows per active student, and the prior year's row is NOT mutated
    public async Task Rollover_appends_a_new_enrollment_per_active_student_and_never_changes_the_prior_year()
    {
        var before = (await EnrollmentsAsync()).Single(e => e.Id == _ashaPriorEnrollmentId);

        Assert.Equal(HttpStatusCode.Accepted, (await RolloverAsync()).StatusCode);

        var all = await EnrollmentsAsync();
        var prior = all.Single(e => e.Id == _ashaPriorEnrollmentId);
        Assert.Equal((before.AcademicYearId, before.StudentId, before.ClassId, before.CreatedAtUtc), (prior.AcademicYearId, prior.StudentId, prior.ClassId, prior.CreatedAtUtc));

        var year = await _factory.QueryAsync(db => db.AcademicYears.IgnoreQueryFilters().AsNoTracking().SingleAsync(y => y.Id == _yearId));
        Assert.NotNull(year.RolledOverAtUtc);
        var next = await _factory.QueryAsync(db => db.AcademicYears.IgnoreQueryFilters().AsNoTracking().SingleAsync(y => y.Id == year.NextAcademicYearId));
        Assert.Equal("2027-28", next.Label);
        Assert.Equal(new DateOnly(2027, 4, 1), next.StartDate);

        Assert.Equal(_grade6, all.Single(e => e.AcademicYearId == next.Id && e.StudentId == _asha).ClassId);
        Assert.Equal(_grade6, all.Single(e => e.AcademicYearId == next.Id && e.StudentId == _ravi).ClassId);
        Assert.Equal(_grade5, all.Single(e => e.AcademicYearId == _yearId && e.StudentId == _ravi).ClassId); // history snapshot written for Ravi
        Assert.Single(all, e => e.AcademicYearId == _yearId && e.StudentId == _asha); // no duplicate snapshot for Asha
        Assert.Equal(_grade6, (await StudentAsync(_asha)).ClassId); // current placement moved
        Assert.DoesNotContain(all, e => e.StudentId == _withdrawn); // inactive students are not rolled
    }

    [Fact] // Q-229: a class with no successor is a final year — no new-year enrolment, student left active
    public async Task Students_in_a_final_year_class_get_no_new_enrollment()
    {
        await RolloverAsync();

        var meena = await StudentAsync(_meena);
        Assert.True(meena.IsActive);
        Assert.Equal(_grade12, meena.ClassId);
        Assert.Single(await EnrollmentsAsync(), e => e.StudentId == _meena); // only the history snapshot
    }

    [Fact] // TC-702 — held back before the rollover: the new-year row keeps the same class
    public async Task A_student_held_back_before_rollover_stays_at_the_same_class_level()
    {
        Assert.Equal(HttpStatusCode.OK, (await HoldBackAsync(_ravi)).StatusCode);

        await RolloverAsync();

        var year = await _factory.QueryAsync(db => db.AcademicYears.IgnoreQueryFilters().AsNoTracking().SingleAsync(y => y.Id == _yearId));
        var all = await EnrollmentsAsync();
        Assert.Equal(_grade5, all.Single(e => e.AcademicYearId == year.NextAcademicYearId && e.StudentId == _ravi).ClassId);
        Assert.Equal(_grade6, all.Single(e => e.AcademicYearId == year.NextAcademicYearId && e.StudentId == _asha).ClassId);
        Assert.Equal(_grade5, (await StudentAsync(_ravi)).ClassId);
    }

    [Fact] // FR-067 "after": the correction is APPENDED; the promotion row the rollover wrote is untouched
    public async Task A_hold_back_after_rollover_appends_a_new_row_rather_than_editing_the_promotion()
    {
        await RolloverAsync();
        var year = await _factory.QueryAsync(db => db.AcademicYears.IgnoreQueryFilters().AsNoTracking().SingleAsync(y => y.Id == _yearId));
        var promotion = (await EnrollmentsAsync()).Single(e => e.AcademicYearId == year.NextAcademicYearId && e.StudentId == _ravi);

        Assert.Equal(HttpStatusCode.OK, (await HoldBackAsync(_ravi)).StatusCode);

        var raviNextYear = (await EnrollmentsAsync()).Where(e => e.AcademicYearId == year.NextAcademicYearId && e.StudentId == _ravi).OrderBy(e => e.CreatedAtUtc).ToList();
        Assert.Equal(2, raviNextYear.Count);
        Assert.Equal((promotion.Id, _grade6), (raviNextYear[0].Id, raviNextYear[0].ClassId)); // unchanged
        Assert.Equal(_grade5, raviNextYear[1].ClassId); // appended correction
        Assert.Equal(_grade5, (await StudentAsync(_ravi)).ClassId);

        Assert.Equal(HttpStatusCode.OK, (await HoldBackAsync(_ravi)).StatusCode); // repeating it appends nothing more
        Assert.Equal(2, (await EnrollmentsAsync()).Count(e => e.AcademicYearId == year.NextAcademicYearId && e.StudentId == _ravi));
    }

    [Fact] // TC-703 — adversarial: another institution's student (and an inactive one) -> ERR-901, nothing recorded
    public async Task A_hold_back_list_with_a_foreign_or_inactive_student_is_rejected_whole()
    {
        var other = await _factory.SeedInstitutionAsync("Other School");
        var otherClass = await _factory.SeedClassAsync(other);
        var foreign = await _factory.SeedStudentAsync(other, otherClass, "SR-F", "R-F");

        foreach (var list in new[] { new[] { _asha, foreign }, new[] { _withdrawn } })
        {
            var response = await HoldBackAsync(list);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("ERR-901", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(0, await _factory.QueryAsync(db => db.RolloverHoldBacks.IgnoreQueryFilters().CountAsync())); // not even Asha
    }

    [Fact] // A year rolls over exactly once: a second request is refused, and a second job run changes nothing
    public async Task Rollover_cannot_run_twice()
    {
        await RolloverAsync();
        var count = (await EnrollmentsAsync()).Count;

        Assert.Equal(HttpStatusCode.Conflict, (await RolloverAsync()).StatusCode);
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RolloverJobRunner>().RunAsync(_yearId, CancellationToken.None);

        Assert.Equal(count, (await EnrollmentsAsync()).Count);
    }

    [Theory] // Leadership only
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    public async Task Non_leadership_cannot_roll_over_or_hold_back(UserRole role)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(Guid.NewGuid(), role, _institutionId));
        Assert.Equal(HttpStatusCode.Forbidden, (await RolloverAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await HoldBackAsync(_asha)).StatusCode);
    }

    [Fact] // P1 retrofit: a class carrying enrolment history cannot be deleted (ERR-306), not a FK 500
    public async Task A_class_with_enrollment_history_cannot_be_deleted()
    {
        await RolloverAsync();
        await _factory.QueryAsync(async db =>
        {
            foreach (var s in await db.Students.IgnoreQueryFilters().Where(s => s.ClassId == _grade6).ToListAsync()) s.ClassId = null;
            return await db.SaveChangesAsync();
        }); // no students placed any more — only history remains

        var response = await _client.DeleteAsync($"/api/v1/classes/{_grade6}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ERR-306", await response.Content.ReadAsStringAsync());
    }

    [Fact] // P1 retrofit: deleting a successor class turns its predecessor into a final year
    public async Task Deleting_a_successor_class_clears_the_predecessor_link()
    {
        var spare = await _factory.SeedClassAsync(_institutionId, "Grade 7", "A");
        await _client.PutAsJsonAsync($"/api/v1/classes/{_grade6}/next-class", new { nextClassId = spare });

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/classes/{spare}")).StatusCode);

        Assert.Null((await _factory.QueryAsync(db => db.Classes.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == _grade6))).NextClassId);
    }

    [Fact] // PF-012 scheduled trigger: only years that have ended in the institution's calendar are rolled
    public async Task The_year_end_job_rolls_over_only_years_that_have_ended()
    {
        var ended = await (await _client.PostAsJsonAsync("/api/v1/academic-years", new { label = "2025-26", startDate = "2025-04-01", endDate = "2026-03-31" })).Content.ReadFromJsonAsync<JsonElement>();

        using var scope = _factory.Services.CreateScope();
        var ran = await scope.ServiceProvider.GetRequiredService<YearEndRolloverJob>().RunAsync(CancellationToken.None); // clock: 2026-09-14

        Assert.Equal(1, ran);
        var years = await _factory.QueryAsync(db => db.AcademicYears.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.NotNull(years.Single(y => y.Id == ended.GetProperty("id").GetGuid()).RolledOverAtUtc);
        Assert.Null(years.Single(y => y.Id == _yearId).RolledOverAtUtc); // still running until 2027-03-31
    }

    [Theory]
    [InlineData("2026-27", "2027-28")]
    [InlineData("2099-00", "2100-01")]
    [InlineData("2026-2027", "2027-2028")]
    [InlineData("Session A", "Session A (next)")]
    public void Next_academic_year_labels_are_derived(string label, string expected) =>
        Assert.Equal(expected, RolloverService.NextLabel(label));
}
