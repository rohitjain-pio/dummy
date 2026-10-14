using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

public enum CorrectionRequestStatus
{
    Pending, // CorrectionPending on the record — awaiting leadership (day 1-30, Teacher request)
    Approved, // API-034 approve: record updated
    Rejected, // API-034 reject: record unchanged, reason recorded
    AutoRejected, // Q-206: the record locked while the request was still pending
    AppliedDirectly, // day 0 by the original recorder, or leadership on day 0-30 (Q-205): no approval step
    Superseded // a leadership direct write replaced the outcome while this request was pending
}

// Q-213 (Rashmi Mehta, 2026-09-14): the correction request is its own table. 04-data-model.md §2.6
// names only AttendanceRecord.correction_status, but API-034 addresses a request by its own id and
// needs a requested outcome, a reason and a resolution — and BR-035 wants rejected requests kept as
// history, which a set of columns on the record would overwrite. Data-model patch pending.
public class CorrectionRequest : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public Guid AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    public AttendanceOutcome RequestedOutcome { get; set; }
    public required string Reason { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public CorrectionRequestStatus Status { get; set; }
    public Guid? ResolvedByUserId { get; set; } // null for AutoRejected (the sweep has no user)
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }

    public string GetAuditEntityId() => Id.ToString();
}
