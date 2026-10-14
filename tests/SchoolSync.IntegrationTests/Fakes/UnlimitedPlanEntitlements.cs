using SchoolSync.Application.Commercial;

namespace SchoolSync.IntegrationTests.Fakes;

// For unit tests of services that consult the tier gates (CT-503) but are not ABOUT them: every
// capability included, no capacity limit. Tier behaviour itself is tested against the real
// PlanEntitlements in the Commercial tests.
public class UnlimitedPlanEntitlements : IPlanEntitlements
{
    public Task<EffectivePlan?> GetAsync(Guid institutionId, CancellationToken ct) =>
        Task.FromResult<EffectivePlan?>(new EffectivePlan(Guid.Empty, "Unlimited", int.MaxValue, int.MaxValue, PlanCapability.All, false));

    public Task<bool> IncludesAsync(Guid institutionId, string capability, CancellationToken ct) => Task.FromResult(true);
    public Task<bool> HasLearnerCapacityAsync(Guid institutionId, int additionalLearners, CancellationToken ct) => Task.FromResult(true);
}
