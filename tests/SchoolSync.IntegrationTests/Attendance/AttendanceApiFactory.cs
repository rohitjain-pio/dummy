using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Common;
using SchoolSync.IntegrationTests.Fakes;

namespace SchoolSync.IntegrationTests.Attendance;

// The shared host plus a settable clock. Attendance is entirely a statement about calendar days in
// the institution's time zone (same day / day 1-30 / locked after 30), which cannot be tested
// honestly against the wall clock. Kept as a subclass so the P1-approved SchoolSyncApiFactory is
// not changed underneath the tests that already rely on TimeProvider.System.
public class AttendanceApiFactory : SchoolSyncApiFactory
{
    // 2026-09-14 06:00 UTC = 2026-09-14 11:30 in Asia/Kolkata (the Q-215 fallback zone).
    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 9, 14, 6, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    private async Task WithDbAsync(Func<SchoolSyncDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>());
    }

    // Test-side assertions only. Outside a request there is no tenant claim, so the query filter
    // hides every tenant-owned row — callers use IgnoreQueryFilters() and filter explicitly.
    public async Task<T> QueryAsync<T>(Func<SchoolSyncDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>());
    }

    // A Teacher row plus the UserAccount that signs in as it (UserAccount.TeacherId, FR-023).
    public async Task<(Guid TeacherId, Guid UserId)> SeedTeacherWithAccountAsync(Guid institutionId, string name = "Meera Rao")
    {
        var teacherId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Teachers.Add(new Teacher
            {
                Id = teacherId,
                InstitutionId = institutionId,
                Name = name,
                ContactEmail = $"{Guid.NewGuid():N}@example.edu",
                Telephone = "+919000000002",
                Gender = "Female",
                Address = "12 Staff Lane",
                JoiningDate = new DateOnly(2024, 6, 1)
            });
            db.UserAccounts.Add(new UserAccount
            {
                Id = userId,
                InstitutionId = institutionId,
                Role = UserRole.Teacher,
                Username = $"teacher-{userId:N}",
                Email = $"{userId:N}@example.edu",
                PasswordHash = "seeded-not-a-real-hash",
                TeacherId = teacherId
            });
            await db.SaveChangesAsync();
        });
        return (teacherId, userId);
    }

    public Task MakeClassTeacherAsync(Guid classId, Guid teacherId) => WithDbAsync(async db =>
    {
        var @class = await db.Classes.IgnoreQueryFilters().SingleAsync(c => c.Id == classId);
        @class.ClassTeacherId = teacherId;
        await db.SaveChangesAsync();
    });

    public async Task<Guid> SeedAccountAsync(Guid institutionId, UserRole role, Guid? studentId = null)
    {
        var userId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = userId,
                InstitutionId = institutionId,
                Role = role,
                Username = $"u-{userId:N}",
                Email = $"{userId:N}@example.edu",
                PasswordHash = "seeded-not-a-real-hash",
                StudentId = studentId
            });
            await db.SaveChangesAsync();
        });
        return userId;
    }

    public Task LinkParentAsync(Guid parentUserId, Guid studentId) => WithDbAsync(async db =>
    {
        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parentUserId, StudentId = studentId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    });

    public Task SeedRecordAsync(Guid institutionId, Guid personId, DateOnly date, AttendanceOutcome outcome,
        Guid recordedByUserId, PersonType type = PersonType.Student) => WithDbAsync(async db =>
    {
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            PersonId = personId,
            PersonType = type,
            AttendanceDate = date,
            Outcome = outcome,
            RecordedByUserId = recordedByUserId,
            RecordedAtUtc = Clock.GetUtcNow().UtcDateTime,
            CorrectionStatus = CorrectionStatus.None
        });
        await db.SaveChangesAsync();
    });
}
