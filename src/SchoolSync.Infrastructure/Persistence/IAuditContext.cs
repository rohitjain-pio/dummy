namespace SchoolSync.Infrastructure.Persistence;

// What the SaveChanges audit interceptor needs about "who is doing this, and how do I correlate
// it" — kept separate from ICurrentTenant, which is about tenant scoping, not identity/tracing.
public interface IAuditContext
{
    // Null only for a system/background process (e.g. a scheduled job) with no signed-in actor —
    // AuditLog.actor_user_id has no NOT NULL constraint precisely to allow this.
    Guid? ActorUserId { get; }

    // company.md §6: "a correlation/trace ID generated at the request boundary and propagated
    // through all downstream calls and log lines for that request."
    string CorrelationId { get; }
}
