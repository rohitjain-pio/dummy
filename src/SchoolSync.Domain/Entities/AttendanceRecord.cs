using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

public enum PersonType { Student, Teacher } // D10 discriminator

public enum AttendanceOutcome { Present, Absent, Late, Leave } // VAL-013

// BRL-034: correction_status is nullable; None/CorrectionPending are the only stored states.
public enum CorrectionStatus { None, CorrectionPending }

// ENT-008. 04-data-model.md §2.6.
// NotYetMarked (BRL-034) is represented as row-absence, never as a stored value — there is no
// "NotYetMarked" member on AttendanceOutcome. A row for (person_id, attendance_date) existing
// at all means a mark was made.
public class AttendanceRecord : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public Guid PersonId { get; set; } // Student.Id or Teacher.Id
    public PersonType PersonType { get; set; }

    public DateOnly AttendanceDate { get; set; }
    public AttendanceOutcome Outcome { get; set; }

    public Guid RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    public CorrectionStatus? CorrectionStatus { get; set; }

    // "locked" (04-data-model.md §2.6) is explicitly a computed/derived value at read time from
    // attendance_date + CFG-004 — it is NOT a stored column here, to avoid drifting out of sync
    // with the clock. See 07-business-logic/attendance.md for the derivation (CT-202).

    public string GetAuditEntityId() => Id.ToString();
}
