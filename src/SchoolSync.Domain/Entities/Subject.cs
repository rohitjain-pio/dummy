using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-007. 04-data-model.md §2.5.
public class Subject : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;
    public required string Name { get; set; }

    public ICollection<SubjectTeacherClassMapping> TeacherClassMappings { get; set; } = [];
}

// FR-022/FR-029/BR-024: join table, composite PK.
public class SubjectTeacherClassMapping
{
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
}
