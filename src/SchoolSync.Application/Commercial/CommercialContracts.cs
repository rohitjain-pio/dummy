using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Commercial;

// BR-063's three tier-reserved capabilities — the only values a plan's capability list may hold.
public static class PlanCapability
{
    public const string AdvancedAnalytics = "AdvancedAnalytics";
    public const string ParentAccess = "ParentAccess";
    public const string CustomReporting = "CustomReporting";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { AdvancedAnalytics, ParentAccess, CustomReporting };
}

public record EffectivePlan(Guid PlanId, string Name, int LearnerCapacity, int StorageAllowanceGb, IReadOnlySet<string> Capabilities, bool IsTrial);

// The single place a tier decision is made (BRL-062/063/073). Server-side, and a SECOND axis after
// RBAC — never instead of it (07-business-logic/configuration-commercial.md cross-reference).
public interface IPlanEntitlements
{
    Task<EffectivePlan?> GetAsync(Guid institutionId, CancellationToken ct);
    Task<bool> IncludesAsync(Guid institutionId, string capability, CancellationToken ct); // BRL-063 -> ERR-802
    Task<bool> HasLearnerCapacityAsync(Guid institutionId, int additionalLearners, CancellationToken ct); // BRL-073 -> ERR-801
}

public record PlanFields(int? LearnerCapacity, int? StorageAllowanceGb, string? SupportLevel, IReadOnlyList<string>? Capabilities);
public record PlanView(Guid Id, string Name, int LearnerCapacity, int StorageAllowanceGb, string SupportLevel, IReadOnlyList<string> Capabilities);
public record SubscriptionView(Guid InstitutionId, Guid SubscriptionPlanId, DateOnly? SubscriptionStartDate);
public record Enquiry(string? FullName, string? Email, string? Institution, string? StudentCountRange, string? Message);

public enum CommercialFailureReason { None, ValidationFailed, NotFound, Forbidden }

public record CommercialResult<T>(bool Succeeded, T? Value, CommercialFailureReason Failure = CommercialFailureReason.None)
{
    public static CommercialResult<T> Ok(T value) => new(true, value);
    public static CommercialResult<T> Fail(CommercialFailureReason reason) => new(false, default, reason);
}

// API-061/062/064 (CT-503) + the Q-227 plan editor.
public interface ISubscriptionService
{
    Task<IReadOnlyList<PlanView>> ListPlansAsync(CancellationToken ct); // API-061
    Task<CommercialResult<bool>> SubmitEnquiryAsync(Enquiry enquiry, CancellationToken ct); // API-062
    Task<CommercialResult<SubscriptionView>> AssignAsync(Guid? institutionId, Guid? planId, DateOnly? startDate, Guid operatorUserId, CancellationToken ct); // API-064
    Task<CommercialResult<PlanView>> UpdatePlanAsync(Guid planId, PlanFields fields, Guid operatorUserId, CancellationToken ct); // Q-227
}

public interface ISubscriptionRepository
{
    // _shared.md §1: "never trust the claim over a fresh DB read ... for security-sensitive operations
    // (subscription change)". The caller's role is re-read from their account row.
    Task<bool> IsActivePlatformOperatorAsync(Guid userId, CancellationToken ct);
    Task<Institution?> FindInstitutionAsync(Guid id, CancellationToken ct);
    Task<SubscriptionPlan?> FindPlanAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// T-004 notification dispatch for API-062. No CRM entity is modelled (ui-design-final.md §1.4).
public interface IEnquiryNotifier
{
    Task NotifyAsync(Enquiry enquiry, CancellationToken ct);
}
