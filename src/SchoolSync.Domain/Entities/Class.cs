using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-006. 04-data-model.md §2.5.
public class Class : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    // Unique composite (institution_id, name, section) — BR-026.
    public required string Name { get; set; }
    public required string Section { get; set; }

    // BR-023: at most one class teacher of at most one class — enforced by a partial unique
    // index (see SchoolSyncDbContext configuration) reading both directions.
    public Guid? ClassTeacherId { get; set; }
    public Teacher? ClassTeacher { get; set; }

    // Q-229 (Rashmi Mehta, 2026-09-14): the class a student moves to at year-end rollover (FR-067's
    // "next class level"). Null = final year — rollover creates no new-year enrolment for its students.
    public Guid? NextClassId { get; set; }

    public ICollection<Student> Students { get; set; } = [];
    public ICollection<SubjectTeacherClassMapping> SubjectTeacherMappings { get; set; } = [];

    // §4 concurrency policy: class edits are an ordinary edit-in-place operation.
    public byte[] RowVersion { get; set; } = [];

    public string GetAuditEntityId() => Id.ToString();
}
