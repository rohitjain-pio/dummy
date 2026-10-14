using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Commercial;

public record TrialView(Guid InstitutionId, Guid TrialPlanId, DateTime TrialExpiresAtUtc);

public enum TrialFailureReason { None, ValidationFailed, NotFound, Forbidden, NotEligible }

public record TrialResult(bool Succeeded, TrialView? Value, TrialFailureReason Failure = TrialFailureReason.None)
{
    public static TrialResult Ok(TrialView value) => new(true, value);
    public static TrialResult Fail(TrialFailureReason reason) => new(false, null, reason);
}

public interface ITrialService
{
    Task<TrialResult> StartAsync(Guid? institutionId, Guid callerUserId, UserRole callerRole, Guid callerInstitutionId, CancellationToken ct); // API-063
}

// CT-504. FR-070/071, BRL-070/071, PF-013, Q-230.
// There is no payment, billing or charge concept anywhere in this class or its dependencies — FR-070
// collects no payment details and FR-071 "never auto-bills". TC-705 asserts that structurally.
public class TrialService(ISubscriptionRepository repository, TimeProvider clock) : ITrialService
{
    public const int TrialDays = 30; // CFG-006
    public static readonly Guid TrialPlanId = new("00000000-0000-0000-0000-000000000002"); // Mid (Q-230)
    public static readonly Guid EntryPlanId = new("00000000-0000-0000-0000-000000000001");

    public async Task<TrialResult> StartAsync(Guid? institutionId, Guid callerUserId, UserRole callerRole, Guid callerInstitutionId, CancellationToken ct)
    {
        if (institutionId is not { } id) return TrialResult.Fail(TrialFailureReason.ValidationFailed);

        // Q-230: never anonymous for an arbitrary id. A leader of THAT institution, or a PlatformOperator
        // whose role is re-read from the account row (_shared.md §1).
        var isOwnLeadership = callerRole is UserRole.Administrator or UserRole.Principal or UserRole.SchoolInCharge && callerInstitutionId == id;
        if (!isOwnLeadership && !(callerRole == UserRole.PlatformOperator && await repository.IsActivePlatformOperatorAsync(callerUserId, ct)))
            return TrialResult.Fail(TrialFailureReason.Forbidden);

        var institution = await repository.FindInstitutionAsync(id, ct);
        if (institution is null) return TrialResult.Fail(TrialFailureReason.NotFound);

        // Once per institution, and only from the Entry tier (a trial of a lower tier than the one
        // already purchased would be a downgrade, not a trial).
        if (institution.TrialStartedAtUtc is not null || institution.SubscriptionPlanId != EntryPlanId)
            return TrialResult.Fail(TrialFailureReason.NotEligible);

        var now = clock.GetUtcNow().UtcDateTime;
        institution.TrialPlanId = TrialPlanId;
        institution.TrialStartedAtUtc = now;
        institution.TrialExpiresAtUtc = now.AddDays(TrialDays);
        await repository.SaveChangesAsync(ct); // audited (Institution is IAuditable)
        return TrialResult.Ok(new TrialView(institution.Id, TrialPlanId, institution.TrialExpiresAtUtc.Value));
    }
}
