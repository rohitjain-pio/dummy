using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Infrastructure.Persistence;

// 07-business-logic/_shared.md §3: "an AuditLog row is written IN THE SAME TRANSACTION, via the
// EF Core SaveChanges interceptor — never by individual handler code." This is the ONLY place in
// the codebase that writes an AuditLog row for an entity change; no controller/service ever adds
// one directly (the adversarial test in AuditLoggingInterceptorTests checks this holds).
public class AuditLoggingInterceptor(IAuditContext auditContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteAuditRows(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        WriteAuditRows(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditRows(DbContext? context)
    {
        if (context is null) return;

        // Snapshot before ChangeTracker.DetectChanges() implicitly runs as part of the entries
        // enumeration below is fine here — EF has already detected changes by the time
        // SavingChanges fires, so State/OriginalValues/CurrentValues below are already correct.
        var governedEntries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (governedEntries.Count == 0) return;

        var actorUserId = auditContext.ActorUserId ?? Guid.Empty; // Guid.Empty = no signed-in actor (system/background)
        var correlationId = auditContext.CorrelationId;
        var now = DateTime.UtcNow;

        foreach (var entry in governedEntries)
        {
            var auditable = (IAuditable)entry.Entity;
            var (action, before, after) = entry.State switch
            {
                EntityState.Added => ("Create", (string?)null, Serialize(entry.CurrentValues)),
                EntityState.Deleted => ("Delete", Serialize(entry.OriginalValues), (string?)null),
                _ => (ClassifyUpdate(entry), Serialize(entry.OriginalValues), Serialize(entry.CurrentValues))
            };

            var institutionId = entry.Entity is ITenantOwned tenantOwned ? tenantOwned.InstitutionId : (Guid?)null;

            context.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorUserId,
                InstitutionId = institutionId,
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = auditable.GetAuditEntityId(),
                BeforeSnapshot = before,
                AfterSnapshot = after,
                TimestampUtc = now,
                CorrelationId = correlationId
            });
        }
    }

    // A Modified state on a soft-deletable entity where IsActive flipped false is a Withdraw/
    // Deactivate in business terms, not a generic "Update" — 07-business-logic/_shared.md §3's
    // action vocabulary explicitly includes Withdraw/Reactivate alongside Create/Update/Delete.
    private static string ClassifyUpdate(EntityEntry entry)
    {
        if (entry.Entity is ISoftDeletable)
        {
            var wasActive = (bool)entry.OriginalValues[nameof(ISoftDeletable.IsActive)]!;
            var isActive = (bool)entry.CurrentValues[nameof(ISoftDeletable.IsActive)]!;
            if (wasActive && !isActive) return "Deactivate";
            if (!wasActive && isActive) return "Reactivate";
        }
        if (entry.Entity is Announcement { IsWithdrawn: true }) return "Withdraw";
        if (entry.Entity is Message { IsWithdrawn: true }) return "Withdraw";
        return "Update";
    }

    private static string Serialize(PropertyValues values)
    {
        var dict = values.Properties.ToDictionary(p => p.Name, p => values[p]);
        return JsonSerializer.Serialize(dict);
    }
}
