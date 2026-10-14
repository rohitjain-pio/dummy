namespace SchoolSync.Infrastructure.Persistence;

// ADR-0007. A background job (Hangfire, CT-402) has no HTTP request and therefore no institution_id
// claim, so the CT-001 tenant filter would hide every row. A job that has loaded its own work item
// enters that item's institution explicitly; HttpCurrentTenant falls back to this ONLY when there is
// no HttpContext, so it can never widen or change the scope of a real request.
public static class BackgroundTenantScope
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public static Guid? InstitutionId => Current.Value;

    public static IDisposable Enter(Guid institutionId)
    {
        var previous = Current.Value;
        Current.Value = institutionId;
        return new Exit(previous);
    }

    private sealed class Exit(Guid? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
