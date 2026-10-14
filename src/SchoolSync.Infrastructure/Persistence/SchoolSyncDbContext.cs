using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Infrastructure.Persistence;

public class SchoolSyncDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;

    public SchoolSyncDbContext(DbContextOptions<SchoolSyncDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectTeacherClassMapping> SubjectTeacherClassMappings => Set<SubjectTeacherClassMapping>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<CorrectionRequest> CorrectionRequests => Set<CorrectionRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RoomMembership> RoomMemberships => Set<RoomMembership>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<ReportRun> ReportRuns => Set<ReportRun>();
    public DbSet<TimetablePeriod> TimetablePeriods => Set<TimetablePeriod>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<InstitutionConfiguration> InstitutionConfigurations => Set<InstitutionConfiguration>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<RolloverHoldBack> RolloverHoldBacks => Set<RolloverHoldBack>();
    public DbSet<ParentLinkCode> ParentLinkCodes => Set<ParentLinkCode>();
    public DbSet<LeadershipCode> LeadershipCodes => Set<LeadershipCode>(); // CT-801, 04-data-model.md 2.12a
    public DbSet<ParentStudentLink> ParentStudentLinks => Set<ParentStudentLink>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PublicInstitutionDirectoryProjection> PublicInstitutionDirectory => Set<PublicInstitutionDirectoryProjection>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyTenantQueryFilterConvention(modelBuilder);
        ConfigureInstitution(modelBuilder);
        ConfigureUserAccount(modelBuilder);
        ConfigureStudent(modelBuilder);
        ConfigureClass(modelBuilder);
        ConfigureSubjectMapping(modelBuilder);
        ConfigureAttendanceRecord(modelBuilder);
        ConfigureRoomMessage(modelBuilder);
        ConfigureTimetablePeriod(modelBuilder);
        ConfigureReportRun(modelBuilder);
        ConfigureInstitutionConfiguration(modelBuilder);
        ConfigureEnrollment(modelBuilder);
        ConfigureParentLinkCode(modelBuilder);
        ConfigureLeadershipCode(modelBuilder);
        ConfigureParentStudentLink(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigurePublicInstitutionDirectory(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        modelBuilder.Entity<IdempotencyRecord>().HasKey(r => r.Key);
        ConfigureSubscriptionPlanSeed(modelBuilder);
    }

    private static void ConfigureRefreshToken(ModelBuilder b)
    {
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.UserAccount).WithMany().HasForeignKey(t => t.UserAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // T-002 / D4: the single shared convention. Every entity implementing ITenantOwned gets this
    // filter applied structurally, by reflection over the model — not per-query discipline, and
    // not re-declared by hand for each of the ~15 tenant-owned entity types. The only bypass is
    // ICurrentTenant.BypassTenantFilter, which is true only inside the one explicitly-marked
    // PlatformOperator cross-tenant path (BR-013) — never a silent global escape hatch.
    private void ApplyTenantQueryFilterConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(SchoolSyncDbContext)
                .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            var filter = (LambdaExpression)method.Invoke(this, null)!;
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    private LambdaExpression BuildTenantFilter<TEntity>() where TEntity : class, ITenantOwned
    {
        Expression<Func<TEntity, bool>> filter = e =>
            _currentTenant.BypassTenantFilter || e.InstitutionId == _currentTenant.InstitutionId;
        return filter;
    }

    private static void ConfigureInstitution(ModelBuilder b)
    {
        b.Entity<Institution>(e =>
        {
            // BR-003: duplicate only when all three match — a DB-level unique constraint on the
            // triple is the correct enforcement of exactly that condition (04-data-model.md §2.1).
            e.HasIndex(i => new { i.Name, i.ContactEmail, i.PostalCode }).IsUnique();
            e.HasIndex(i => new { i.City, i.Region }); // directory search, D12
            e.HasOne(i => i.SubscriptionPlan).WithMany()
                .HasForeignKey(i => i.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
            e.Property(i => i.RowVersion).IsRowVersion();
        });
    }

    private static void ConfigureUserAccount(ModelBuilder b)
    {
        b.Entity<UserAccount>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.InstitutionId);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(30);
            e.HasOne(u => u.Institution).WithMany().HasForeignKey(u => u.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
            // Restrict, not SetNull: SQL Server rejects multiple cascade/set-null paths
            // converging on the same table, and since Institution/Student/Teacher/UserAccount
            // all use soft-delete (D8) there is no hard-delete path that would ever need this
            // to cascade in the first place.
            e.HasOne(u => u.Student).WithMany().HasForeignKey(u => u.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.Teacher).WithMany().HasForeignKey(u => u.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureStudent(ModelBuilder b)
    {
        b.Entity<Student>(e =>
        {
            e.HasIndex(s => new { s.InstitutionId, s.SrNumber }).IsUnique();
            e.HasIndex(s => new { s.InstitutionId, s.RollNumber }).IsUnique();
            e.HasIndex(s => new { s.InstitutionId, s.ClassId });
            e.HasOne(s => s.Class).WithMany(c => c.Students).HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(s => s.RowVersion).IsRowVersion();
        });
    }

    private static void ConfigureClass(ModelBuilder b)
    {
        b.Entity<Class>(e =>
        {
            e.HasIndex(c => new { c.InstitutionId, c.Name, c.Section }).IsUnique();
            // BR-023: at most one class teacher of at most one class, from the class side.
            e.HasIndex(c => c.ClassTeacherId).IsUnique()
                .HasFilter("[ClassTeacherId] IS NOT NULL");
            e.HasOne(c => c.ClassTeacher).WithMany().HasForeignKey(c => c.ClassTeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            // Q-229 successor. NoAction (SQL Server refuses a self-referencing SET NULL cascade);
            // ClassRepository.RemoveClassAsync clears predecessors before deleting.
            e.HasOne<Class>().WithMany().HasForeignKey(c => c.NextClassId).OnDelete(DeleteBehavior.NoAction);
            e.Property(c => c.RowVersion).IsRowVersion();
        });
    }

    private static void ConfigureSubjectMapping(ModelBuilder b)
    {
        b.Entity<SubjectTeacherClassMapping>(e =>
        {
            e.HasKey(m => new { m.SubjectId, m.TeacherId, m.ClassId });
            e.HasOne(m => m.Subject).WithMany(s => s.TeacherClassMappings)
                .HasForeignKey(m => m.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Teacher).WithMany().HasForeignKey(m => m.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Class).WithMany(c => c.SubjectTeacherMappings)
                .HasForeignKey(m => m.ClassId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAttendanceRecord(ModelBuilder b)
    {
        b.Entity<AttendanceRecord>(e =>
        {
            // D10 / BRL-031: the upsert key. A second submission for the same key updates,
            // never inserts a duplicate row.
            e.HasIndex(a => new { a.PersonId, a.AttendanceDate }).IsUnique();
            e.HasIndex(a => new { a.InstitutionId, a.AttendanceDate }); // FR-037 retrieval
            e.Property(a => a.PersonType).HasConversion<string>().HasMaxLength(10);
            e.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(10);
            e.Property(a => a.CorrectionStatus).HasConversion<string>().HasMaxLength(20);
        });

        // Q-213. Cascade from the record (API-035 removes an entry together with its request
        // history — the AuditLog keeps both before-images); Restrict from Institution, because
        // Institution -> AttendanceRecord -> CorrectionRequest is already a cascade path and SQL
        // Server rejects a second one (the same conflict CT-001 hit on UserAccount).
        b.Entity<CorrectionRequest>(e =>
        {
            e.HasOne(r => r.AttendanceRecord).WithMany().HasForeignKey(r => r.AttendanceRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Institution).WithMany().HasForeignKey(r => r.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.RequestedOutcome).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Reason).HasMaxLength(500);
            e.Property(r => r.ResolutionNote).HasMaxLength(500);
            // E3: at most one Pending request per record, enforced by the database as well as
            // the service (the InMemory test provider ignores filtered indexes).
            e.HasIndex(r => r.AttendanceRecordId).IsUnique().HasFilter("[Status] = 'Pending'");
            e.HasIndex(r => new { r.Status, r.RequestedAtUtc }); // the lock sweep's scan
        });
    }

    private static void ConfigureRoomMessage(ModelBuilder b)
    {
        b.Entity<Room>(e =>
        {
            e.HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RoomMembership>(e => // Q-219
        {
            e.HasKey(m => new { m.RoomId, m.UserId });
            e.HasOne(m => m.Room).WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.UserId);
        });
        b.Entity<Message>(e =>
        {
            e.Property(m => m.Content).HasMaxLength(4000); // VAL-020
            e.HasIndex(m => new { m.RoomId, m.AuthorId, m.ClientMessageId }).IsUnique()
                .HasFilter("[ClientMessageId] IS NOT NULL"); // EVT-001 de-duplication
            e.HasIndex(m => new { m.RoomId, m.SentAtUtc }); // API-044 history order
            e.HasOne(m => m.Room).WithMany(r => r.Messages).HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Author).WithMany().HasForeignKey(m => m.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Meeting>(e =>
        {
            e.HasOne(m => m.Room).WithMany(r => r.Meetings).HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.RoomId).IsUnique().HasFilter("[EndedAtUtc] IS NULL"); // Q-220: one live meeting per room
        });
    }

    private static void ConfigureReportRun(ModelBuilder b)
    {
        b.Entity<ReportRun>(e =>
        {
            e.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(30);
            e.Property(r => r.Format).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.FailureReason).HasMaxLength(1000);
            e.HasIndex(r => new { r.InstitutionId, r.RequestedAtUtc }); // API-051 history, newest first
        });
    }

    private static void ConfigureTimetablePeriod(ModelBuilder b)
    {
        b.Entity<TimetablePeriod>(e =>
        {
            // BR-053/054/055: second line of defense behind the application-level check.
            e.HasIndex(t => new { t.TeacherId, t.DayOfWeek, t.TimeSlot }).IsUnique();
            e.HasIndex(t => new { t.ClassId, t.DayOfWeek, t.TimeSlot }).IsUnique();
            e.HasIndex(t => new { t.Room, t.InstitutionId, t.DayOfWeek, t.TimeSlot }).IsUnique();
            e.Property(t => t.DayOfWeek).HasConversion<string>();
            e.HasOne(t => t.Class).WithMany().HasForeignKey(t => t.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Subject).WithMany().HasForeignKey(t => t.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Teacher).WithMany().HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInstitutionConfiguration(ModelBuilder b)
    {
        b.Entity<InstitutionConfiguration>(e =>
        {
            e.HasKey(c => c.InstitutionId); // PK+FK, one row per institution
            e.HasOne(c => c.Institution).WithOne().HasForeignKey<InstitutionConfiguration>(c => c.InstitutionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.RowVersion).IsRowVersion();
        });
        b.Entity<Term>(e =>
        {
            e.HasOne(t => t.AcademicYear).WithMany(a => a.Terms).HasForeignKey(t => t.AcademicYearId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureEnrollment(ModelBuilder b)
    {
        // Append-only (BR-060) — no update path is exposed anywhere in the application layer;
        // enforced by convention (no repository method updates this entity), not by a DB trigger.
        b.Entity<Enrollment>(e =>
        {
            e.HasOne(en => en.AcademicYear).WithMany(a => a.Enrollments).HasForeignKey(en => en.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Student).WithMany().HasForeignKey(en => en.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Class).WithMany().HasForeignKey(en => en.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(en => new { en.AcademicYearId, en.StudentId, en.CreatedAtUtc }); // latest placement per year
        });
        b.Entity<RolloverHoldBack>(e => // CT-502 / API-060
        {
            e.HasKey(h => new { h.AcademicYearId, h.StudentId });
            e.HasOne<AcademicYear>().WithMany().HasForeignKey(h => h.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Student>().WithMany().HasForeignKey(h => h.StudentId).OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureParentLinkCode(ModelBuilder b)
    {
        b.Entity<ParentLinkCode>(e =>
        {
            // "unique while status='Issued'" (04-data-model.md §2.12) — Issued means
            // unconsumed + unrevoked. A filtered unique index expresses exactly that.
            e.HasIndex(p => p.Code).IsUnique()
                .HasFilter("[ConsumedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");
            e.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // CT-801 / U38. 04-data-model.md §2.12a/§2.12b.
    private static void ConfigureLeadershipCode(ModelBuilder b)
    {
        b.Entity<LeadershipCode>(e =>
        {
            // §2.12b(b): Base64url of a full, untruncated HMAC-SHA256 is exactly 43 ASCII chars.
            // char, not nvarchar — the alphabet is ASCII by construction, so a fixed-width
            // non-Unicode column halves the index page cost and makes the length a constraint
            // rather than a convention.
            e.Property(c => c.CodeLookup).HasColumnType("char(43)").IsRequired();
            e.Property(c => c.CodeHash).IsRequired();
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(c => c.GrantedRole).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(c => c.RowVersion).IsRowVersion();

            // "Unique while status='Issued'" — this filtered index is what makes one code redeem
            // exactly once under concurrency, and filtering on Status keeps the index from ever
            // spanning two key epochs after a CFG-SEC-06 rotation (§2.12b(c) step 2).
            e.HasIndex(c => c.CodeLookup).IsUnique().HasFilter("[Status] = 'Issued'");

            e.HasOne(c => c.Institution).WithMany().HasForeignKey(c => c.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Q-241 / U43. Nullable, NO_ACTION, filtered index. NULL means the account predates 1.0.5
        // and its provenance is not attributable — which is exactly the query a human reviewer
        // runs. No back-fill: inventing provenance would defeat the point of asking.
        b.Entity<UserAccount>(e =>
        {
            e.HasIndex(u => u.ProvisionedByCodeId).HasFilter("[ProvisionedByCodeId] IS NOT NULL");
            e.HasOne<LeadershipCode>().WithMany().HasForeignKey(u => u.ProvisionedByCodeId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureParentStudentLink(ModelBuilder b)
    {
        b.Entity<ParentStudentLink>(e =>
        {
            e.HasKey(p => new { p.ParentUserId, p.StudentId });
            e.HasOne(p => p.ParentUser).WithMany().HasForeignKey(p => p.ParentUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder b)
    {
        // Deliberately no query filter (AuditLog is not ITenantOwned) and no update/delete path
        // anywhere in the application layer — append-only per NFR-033.
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.InstitutionId);
            e.HasIndex(a => a.TimestampUtc);
        });
    }

    // BR-061: exactly 3 seed rows (Entry, Mid, Bespoke). Fixed, well-known GUIDs so the seed is
    // deterministic across environments/migrations rather than a new random id every apply.
    public static readonly Guid EntryPlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid MidPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid BespokePlanId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static void ConfigureSubscriptionPlanSeed(ModelBuilder b)
    {
        b.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan { Id = EntryPlanId, Name = "Entry", LearnerCapacity = 200, StorageAllowanceGb = 5, SupportLevel = "Standard", CapabilitiesJson = "[]" },
            new SubscriptionPlan { Id = MidPlanId, Name = "Mid", LearnerCapacity = 1000, StorageAllowanceGb = 25, SupportLevel = "Priority", CapabilitiesJson = "[\"AdvancedAnalytics\"]" },
            new SubscriptionPlan { Id = BespokePlanId, Name = "Bespoke", LearnerCapacity = int.MaxValue, StorageAllowanceGb = 500, SupportLevel = "Dedicated", CapabilitiesJson = "[\"AdvancedAnalytics\",\"ParentAccess\",\"CustomReporting\"]" }
        );
    }

    private static void ConfigurePublicInstitutionDirectory(ModelBuilder b)
    {
        b.Entity<PublicInstitutionDirectoryProjection>(e =>
        {
            e.HasKey(p => p.InstitutionId);
            e.HasIndex(p => new { p.City, p.Region }); // D12 search
        });
    }
}
