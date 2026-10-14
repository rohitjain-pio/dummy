using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// 05-api-contracts/operations.md "Report types built (traced)". There is deliberately no Student
// Satisfaction or Course Completion member (TC-609; 01-source-catalogue.md §5).
public enum ReportType { ClassAttendance, StudentPerformance, CourseAnalytics, TeacherPerformance }

// VAL-022 / D7 name PDF and CSV. Q-226 (Rashmi Mehta, 2026-09-14): CSV only this build — PDF is a
// recorded deviation, so the enum carries only what can actually be produced.
public enum ReportFormat { Csv }

// PF-010: Pending -> Ready, or Failed once retries are exhausted (TC-604 — never silently dropped).
public enum ReportRunStatus { Pending, Ready, Failed }

// ENT-013. 04-data-model.md §2.9 + D11: metadata row here, the file itself in Blob Storage.
// CT-402 adds what PF-010's lifecycle needs and §2.9 omits: status, the request parameters the job
// runs from, and a nullable file pointer / generated time (there is no file while Pending).
public class ReportRun : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public ReportType ReportType { get; set; }
    public ReportFormat Format { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public Guid? ScopeClassId { get; set; } // null = whole school (leadership only)
    public Guid? ScopeTeacherUserId { get; set; } // Teacher Performance requested by a Teacher: themselves only (BR-051)

    public ReportRunStatus Status { get; set; }
    public Guid GeneratedByUserId { get; set; } // the requester
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? GeneratedAtUtc { get; set; } // FR-055 "last-generated time"; set when Ready
    public string? BlobUri { get; set; }
    public string? FailureReason { get; set; }

    public string GetAuditEntityId() => Id.ToString();
}
