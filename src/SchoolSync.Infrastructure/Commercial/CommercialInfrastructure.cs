using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolSync.Application.Commercial;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Commercial;

// BRL-062/063/073. Institution and SubscriptionPlan are not tenant-owned, so they are read by id.
public class PlanEntitlements(SchoolSyncDbContext db, TimeProvider clock) : IPlanEntitlements
{
    public async Task<EffectivePlan?> GetAsync(Guid institutionId, CancellationToken ct)
    {
        var institution = await db.Institutions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == institutionId, ct);
        if (institution is null) return null;

        // Q-230: an unexpired trial grants its trial plan; otherwise the assigned subscription plan.
        var onTrial = institution.TrialPlanId is not null && institution.TrialExpiresAtUtc > clock.GetUtcNow().UtcDateTime;
        var planId = onTrial ? institution.TrialPlanId!.Value : institution.SubscriptionPlanId;
        var plan = await db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        return plan is null
            ? null
            : new EffectivePlan(plan.Id, plan.Name, plan.LearnerCapacity, plan.StorageAllowanceGb, SubscriptionService.Capabilities(plan).ToHashSet(StringComparer.Ordinal), onTrial);
    }

    public async Task<bool> IncludesAsync(Guid institutionId, string capability, CancellationToken ct) =>
        await GetAsync(institutionId, ct) is { } plan && plan.Capabilities.Contains(capability);

    // BRL-073: checked BEFORE commit. Active learners only — a withdrawn student does not occupy a
    // seat (reactivating one does, which is why reactivation passes through this gate too). Q-210's
    // provisional E1 reading is what falls out: an institution already over a newly-lowered limit
    // keeps its existing learners but cannot add another.
    public async Task<bool> HasLearnerCapacityAsync(Guid institutionId, int additionalLearners, CancellationToken ct)
    {
        if (await GetAsync(institutionId, ct) is not { } plan) return false;
        var active = await db.Students.CountAsync(s => s.InstitutionId == institutionId && s.IsActive, ct);
        return (long)active + additionalLearners <= plan.LearnerCapacity;
    }
}

public class SubscriptionRepository(SchoolSyncDbContext db) : ISubscriptionRepository
{
    // PlatformOperator requests run with the tenant filter bypassed (BR-013, HttpCurrentTenant), so
    // the operator's own account row is visible here without IgnoreQueryFilters().
    public Task<bool> IsActivePlatformOperatorAsync(Guid userId, CancellationToken ct) =>
        db.UserAccounts.AnyAsync(u => u.Id == userId && u.IsActive && u.Role == UserRole.PlatformOperator, ct);

    public Task<Institution?> FindInstitutionAsync(Guid id, CancellationToken ct) => db.Institutions.FirstOrDefaultAsync(i => i.Id == id, ct);
    public Task<SubscriptionPlan?> FindPlanAsync(Guid id, CancellationToken ct) => db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
    public async Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken ct) => await db.SubscriptionPlans.AsNoTracking().ToListAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

// API-062: no email channel is configured this release (T-004 ACS Email is unbound), so the enquiry
// is written to the log at Warning — visible in the ops log stream rather than silently dropped.
// Recorded as a finding: it is not a delivery mechanism.
public class LoggingEnquiryNotifier(ILogger<LoggingEnquiryNotifier> logger) : IEnquiryNotifier
{
    public Task NotifyAsync(Enquiry e, CancellationToken ct)
    {
        logger.LogWarning("Public enquiry received from {FullName} <{Email}> ({Institution}, {StudentCountRange}): {Message}",
            e.FullName, e.Email, e.Institution, e.StudentCountRange, e.Message);
        return Task.CompletedTask;
    }
}
