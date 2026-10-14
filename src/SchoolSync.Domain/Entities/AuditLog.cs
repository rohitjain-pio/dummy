namespace SchoolSync.Domain.Entities;

// 04-data-model.md §2.12 + §3. Deliberately does NOT implement ITenantOwned: it carries an
// InstitutionId column, but per §3's cross-cutting policy it is "cross-tenant-queryable only via
// the PlatformOperator path" — the automatic global query filter must not apply here, since the
// whole point of an audit log is that it survives and remains inspectable independent of the
// normal per-tenant view. Append-only: no UPDATE/DELETE endpoint exists (NFR-033).
public class AuditLog
{
    public Guid Id { get; set; }

    public Guid ActorUserId { get; set; }
    public Guid? InstitutionId { get; set; }

    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }

    public string? BeforeSnapshot { get; set; }
    public string? AfterSnapshot { get; set; }

    public DateTime TimestampUtc { get; set; }
    public required string CorrelationId { get; set; }
}
