using System.Text.Json;
using SchoolSync.Application.Validation;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Commercial;

// CT-503. BRL-061/062/064, API-061/062/064, Q-227.
public class SubscriptionService(ISubscriptionRepository repository, IEnquiryNotifier notifier) : ISubscriptionService
{
    public static IReadOnlyList<string> Capabilities(SubscriptionPlan plan)
    {
        try { return JsonSerializer.Deserialize<List<string>>(plan.CapabilitiesJson) ?? []; }
        catch (JsonException) { return []; }
    }

    private static PlanView ToView(SubscriptionPlan p) => new(p.Id, p.Name, p.LearnerCapacity, p.StorageAllowanceGb, p.SupportLevel, Capabilities(p));

    public async Task<IReadOnlyList<PlanView>> ListPlansAsync(CancellationToken ct) =>
        (await repository.ListPlansAsync(ct)).OrderBy(p => p.LearnerCapacity).Select(ToView).ToList();

    public async Task<CommercialResult<bool>> SubmitEnquiryAsync(Enquiry e, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(e.FullName) || !ContactFormats.IsValidEmail(e.Email) || string.IsNullOrWhiteSpace(e.Message))
            return CommercialResult<bool>.Fail(CommercialFailureReason.ValidationFailed);
        await notifier.NotifyAsync(e, ct);
        return CommercialResult<bool>.Ok(true);
    }

    public async Task<CommercialResult<SubscriptionView>> AssignAsync(Guid? institutionId, Guid? planId, DateOnly? startDate, Guid operatorUserId, CancellationToken ct)
    {
        static CommercialResult<SubscriptionView> Fail(CommercialFailureReason r) => CommercialResult<SubscriptionView>.Fail(r);

        if (!await repository.IsActivePlatformOperatorAsync(operatorUserId, ct)) return Fail(CommercialFailureReason.Forbidden); // BRL-064
        if (institutionId is not { } iid || planId is not { } pid || startDate is not { } start) return Fail(CommercialFailureReason.ValidationFailed);

        var institution = await repository.FindInstitutionAsync(iid, ct);
        var plan = await repository.FindPlanAsync(pid, ct);
        if (institution is null || plan is null) return Fail(CommercialFailureReason.NotFound); // BRL-061: one of the three seeded tiers

        institution.SubscriptionPlanId = plan.Id;
        institution.SubscriptionStartDate = start;
        EndTrial(institution); // PF-013 path 3a: a purchased plan closes any running trial
        await repository.SaveChangesAsync(ct); // NFR-032: audited (Institution is IAuditable)
        return CommercialResult<SubscriptionView>.Ok(new SubscriptionView(institution.Id, institution.SubscriptionPlanId, institution.SubscriptionStartDate));
    }

    // PF-013 path 3a (CT-504): a purchased plan closes a running trial, so the reversion job has
    // nothing to revert. TrialStartedAtUtc stays set — the institution has used its one trial.
    internal static void EndTrial(Institution institution)
    {
        institution.TrialPlanId = null;
        institution.TrialExpiresAtUtc = null;
    }

    public async Task<CommercialResult<PlanView>> UpdatePlanAsync(Guid planId, PlanFields f, Guid operatorUserId, CancellationToken ct)
    {
        static CommercialResult<PlanView> Fail(CommercialFailureReason r) => CommercialResult<PlanView>.Fail(r);

        if (!await repository.IsActivePlatformOperatorAsync(operatorUserId, ct)) return Fail(CommercialFailureReason.Forbidden);
        var plan = await repository.FindPlanAsync(planId, ct);
        if (plan is null) return Fail(CommercialFailureReason.NotFound);
        if (f.LearnerCapacity is not > 0 || f.StorageAllowanceGb is not > 0 || string.IsNullOrWhiteSpace(f.SupportLevel)
            || f.Capabilities is null || f.Capabilities.Any(c => !PlanCapability.All.Contains(c)))
            return Fail(CommercialFailureReason.ValidationFailed); // BRL-063: only the three named capabilities exist

        // The plan NAME is not editable: BRL-061 fixes the three tiers; Q-227 makes their limits data.
        plan.LearnerCapacity = f.LearnerCapacity.Value;
        plan.StorageAllowanceGb = f.StorageAllowanceGb.Value;
        plan.SupportLevel = f.SupportLevel.Trim();
        plan.CapabilitiesJson = JsonSerializer.Serialize(f.Capabilities.Distinct().ToList());
        await repository.SaveChangesAsync(ct); // audited (SubscriptionPlan is IAuditable)
        return CommercialResult<PlanView>.Ok(ToView(plan));
    }
}
