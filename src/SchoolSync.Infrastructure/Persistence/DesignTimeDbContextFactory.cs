using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolSync.Infrastructure.Persistence;

// Used only by `dotnet ef migrations add` / `database update` at design time — never by the
// running application (the Api layer registers the real ICurrentTenant per-request, CT-002).
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchoolSyncDbContext>
{
    private sealed class DesignTimeCurrentTenant : ICurrentTenant
    {
        public Guid? InstitutionId => null;
        public bool BypassTenantFilter => true; // design-time model building only
    }

    public SchoolSyncDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>()
            .UseSqlServer("Server=localhost;Database=SchoolSync;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new SchoolSyncDbContext(options, new DesignTimeCurrentTenant());
    }
}
