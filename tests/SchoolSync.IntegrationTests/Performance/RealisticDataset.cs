using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Performance;

/// <summary>
/// CT-701. The realistic dataset the NFR measurements run against.
///
/// "Realistic" is the whole point of the DoD, so these are sized from the product's own tier
/// definitions rather than picked for convenience: the Mid tier admits 1,000 learners
/// (04-data-model.md §2.10 seed), so a single institution here carries 1,000 students across 40
/// classes with 60 teachers, and 30 school days of attendance — 30,000 attendance rows, which is
/// roughly what a term of real use looks like. A 3-row fixture would make every target pass and
/// prove nothing.
/// </summary>
public static class RealisticDataset
{
    public const int Classes = 40;
    public const int StudentsPerClass = 25;      // 40 x 25 = 1,000 learners, the Mid tier ceiling
    public const int Teachers = 60;
    public const int AttendanceDays = 30;
    public const int Announcements = 200;
    public const int Messages = 500;

    public sealed record Seeded(
        Guid InstitutionId,
        Guid LeadershipUserId,
        Guid TeacherUserId,
        Guid FirstClassId,
        Guid FirstStudentId,
        Guid RoomId,
        IReadOnlyList<Guid> ClassIds,
        IReadOnlyList<Guid> StudentIds,
        IReadOnlyDictionary<Guid, Guid> ClassTeacherUserIds,
        int AttendanceRowCount);

    public static async Task<Seeded> SeedAsync(
        PerfApiFactory factory,
        string name = "Perf Institution",
        int classes = Classes,
        int attendanceDays = AttendanceDays)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();

        var institutionId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        db.Institutions.Add(new Institution
        {
            Id = institutionId,
            Name = name,
            RegistrationNumber = "REG-" + suffix,
            ContactEmail = suffix + "@example.edu",
            Telephone = "+919000000000",
            PostalAddress = "1 Perf St",
            PostalCode = "560001",
            City = "Perf City",
            Region = "Perf Region",
            SubscriptionPlanId = SchoolSyncDbContext.BespokePlanId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        });

        var leadershipUserId = Guid.NewGuid();
        var teacherUserId = Guid.NewGuid();
        db.UserAccounts.AddRange(
            NewUser(leadershipUserId, institutionId, UserRole.Principal, "head-" + suffix),
            NewUser(teacherUserId, institutionId, UserRole.Teacher, "teach-" + suffix));

        // Teachers first, each with the UserAccount that is linked to them (FR-023). NFR-022's peak
        // is N *different* teachers each marking their own class at school-day start, so every
        // class needs a real class teacher who is genuinely entitled to mark it under BR-020 —
        // otherwise the load test measures 403s.
        var teacherIds = new List<Guid>();
        var teacherUserIds = new List<Guid>();
        for (var t = 0; t < Teachers; t++)
        {
            var teacherId = Guid.NewGuid();
            teacherIds.Add(teacherId);
            db.Teachers.Add(new Teacher
            {
                Id = teacherId,
                InstitutionId = institutionId,
                Name = "Teacher " + t.ToString("D3"),
                ContactEmail = "t" + t.ToString("D3") + "." + suffix + "@example.edu",
                Telephone = "+919000000001",
                Gender = t % 2 == 0 ? "Female" : "Male",
                Address = t + " Staff Row",
                JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3),
                IsActive = true
            });

            var teacherUser = NewUser(Guid.NewGuid(), institutionId, UserRole.Teacher, "t" + t.ToString("D3") + "-" + suffix);
            teacherUser.TeacherId = teacherId;
            teacherUserIds.Add(teacherUser.Id);
            db.UserAccounts.Add(teacherUser);
        }

        var classIds = new List<Guid>();
        var classTeacherUserIds = new Dictionary<Guid, Guid>();
        for (var c = 0; c < classes; c++)
        {
            var id = Guid.NewGuid();
            classIds.Add(id);
            var section = ((char)('A' + (c % 4))).ToString();
            classTeacherUserIds[id] = teacherUserIds[c % Teachers];
            db.Classes.Add(new Class
            {
                Id = id,
                InstitutionId = institutionId,
                Name = "Grade " + ((c / 4) + 1),
                Section = section,
                ClassTeacherId = teacherIds[c % Teachers]
            });
        }

        var studentIds = new List<Guid>();
        var n = 0;
        foreach (var classId in classIds)
        {
            for (var s = 0; s < StudentsPerClass; s++, n++)
            {
                var id = Guid.NewGuid();
                studentIds.Add(id);
                db.Students.Add(new Student
                {
                    Id = id,
                    InstitutionId = institutionId,
                    ClassId = classId,
                    SrNumber = "SR" + n.ToString("D5"),
                    RollNumber = "R" + n.ToString("D5"),
                    Name = "Student " + n.ToString("D4"),
                    DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
                    Gender = n % 2 == 0 ? "Female" : "Male",
                    Email = "s" + n.ToString("D5") + "." + suffix + "@example.edu",
                    IsActive = true
                });
            }
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Attendance is the big table, and the one NFR-022 and the reporting reads care about.
        // Inserted in day-sized batches through the real schema so the real indexes are exercised.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attendanceRows = 0;
        for (var d = 1; d <= attendanceDays; d++)
        {
            var date = today.AddDays(-d);
            foreach (var studentId in studentIds)
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    InstitutionId = institutionId,
                    PersonId = studentId,
                    PersonType = PersonType.Student,
                    AttendanceDate = date,
                    Outcome = (attendanceRows % 17) switch
                    {
                        0 => AttendanceOutcome.Absent,
                        1 => AttendanceOutcome.Late,
                        2 => AttendanceOutcome.Leave,
                        _ => AttendanceOutcome.Present
                    },
                    RecordedByUserId = teacherUserId,
                    RecordedAtUtc = DateTime.UtcNow.AddDays(-d)
                });
                attendanceRows++;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        for (var a = 0; a < Announcements; a++)
        {
            db.Announcements.Add(new Announcement
            {
                Id = Guid.NewGuid(),
                InstitutionId = institutionId,
                Title = "Announcement " + a.ToString("D3"),
                Content = "Body of announcement " + a.ToString("D3") + ". " + new string('x', 200),
                EffectiveDate = today.AddDays(-(a % 60)),
                AuthorId = leadershipUserId,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-(a % 60))
            });
        }

        var roomId = Guid.NewGuid();
        db.Rooms.Add(new Room
        {
            Id = roomId,
            InstitutionId = institutionId,
            Name = "Perf Room",
            PasscodeHash = "not-a-real-hash",
            CreatedByUserId = leadershipUserId,
            IsActive = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        for (var m = 0; m < Messages; m++)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                AuthorId = m % 2 == 0 ? leadershipUserId : teacherUserId,
                Content = "Message " + m.ToString("D4"),
                SentAtUtc = DateTime.UtcNow.AddMinutes(-m)
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Seeded(
            institutionId,
            leadershipUserId,
            teacherUserId,
            classIds[0],
            studentIds[0],
            roomId,
            classIds,
            studentIds,
            classTeacherUserIds,
            attendanceRows);
    }

    private static UserAccount NewUser(Guid id, Guid institutionId, UserRole role, string username) => new()
    {
        Id = id,
        InstitutionId = institutionId,
        Role = role,
        Username = username,
        Email = username + "@example.edu",
        PasswordHash = "seeded-not-a-real-hash",
        IsActive = true
    };
}
