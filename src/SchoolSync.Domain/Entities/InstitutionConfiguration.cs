using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-016. 04-data-model.md §2.11. PK+FK on InstitutionId — one row per institution.
public class InstitutionConfiguration : ITenantOwned, IAuditable
{
    public Guid InstitutionId { get; set; } // PK and FK
    public Institution Institution { get; set; } = null!;

    public required string AcademicYear { get; set; } // FR-063
    public required string TimeZone { get; set; }
    public required string Language { get; set; } // Q-209: English only this release
    public required string GradingScale { get; set; } // FR-066
    public required string CourseCategories { get; set; }
    public required string AssessmentTypes { get; set; }
    public required string ReportCardFormat { get; set; }
    public required string NotificationPreferencesJson { get; set; } // FR-065

    // §4 concurrency policy: configuration is an ordinary edit-in-place operation.
    public byte[] RowVersion { get; set; } = [];

    // The PK/FK is InstitutionId itself (one row per institution) — see summary above.
    public string GetAuditEntityId() => InstitutionId.ToString();
}

// ENT-017 (part 1). 04-data-model.md §2.11.
public class AcademicYear : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Label { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ICollection<Term> Terms { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];

    // CT-502 rollover state (PF-012). RolloverRequestedAtUtc is set by API-059 or the year-end job;
    // RolledOverAtUtc/NextAcademicYearId once the job has run — a year rolls over exactly once.
    public DateTime? RolloverRequestedAtUtc { get; set; }
    public DateTime? RolledOverAtUtc { get; set; }
    public Guid? NextAcademicYearId { get; set; }
}

// CT-502, API-060 (FR-067): a student held back from promotion at the end of an academic year.
// Recorded before or after that year's rollover runs.
public class RolloverHoldBack : ITenantOwned
{
    public Guid AcademicYearId { get; set; } // the year being rolled over FROM
    public Guid StudentId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// ENT-017 (part 2). 04-data-model.md §2.11.
public class Term
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public required string Label { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

// 04-data-model.md §2.11. Append-only — never updated after insert (BR-060). A class-placement
// change creates a new row; the prior one is retained as history.
public class Enrollment
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
