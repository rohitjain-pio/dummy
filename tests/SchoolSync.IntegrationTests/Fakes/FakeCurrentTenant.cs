using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.IntegrationTests.Fakes;

public class FakeCurrentTenant : ICurrentTenant
{
    public Guid? InstitutionId { get; set; }
    public bool BypassTenantFilter { get; set; }
}
