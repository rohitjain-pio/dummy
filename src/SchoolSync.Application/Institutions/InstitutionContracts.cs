namespace SchoolSync.Application.Institutions;

public record InstitutionFields(
    string Name, string RegistrationNumber, string ContactEmail, string Telephone,
    string PostalAddress, string PostalCode, string City, string Region);

public record InstitutionResult(
    Guid Id, string Name, string RegistrationNumber, string ContactEmail, string Telephone,
    string PostalAddress, string PostalCode, string City, string Region,
    bool IsActive, DateTime? DeactivatedAtUtc, Guid SubscriptionPlanId);

// Q-235 (Rashmi Mehta, 2026-09-14): Id added so the UI-005 register typeahead can submit institutionId.
// An opaque key is not PII — NFR-016 still excludes contactEmail/postalAddress/registrationNumber.
public record DirectoryEntry(Guid Id, string Name, string City, string Region);

public enum InstitutionFailureReason
{
    None,
    ValidationFailed, // ERR-901
    Duplicate, // ERR-101 (BRL-003)
    Forbidden, // ERR-102
    NotFound // ERR-902
}

public record InstitutionResult2<T>(bool Succeeded, T? Value, InstitutionFailureReason Failure = InstitutionFailureReason.None)
{
    public static InstitutionResult2<T> Ok(T value) => new(true, value);
    public static InstitutionResult2<T> Fail(InstitutionFailureReason reason) => new(false, default, reason);
}

public interface IInstitutionService
{
    Task<InstitutionResult2<InstitutionResult>> RegisterAsync(InstitutionFields fields, CancellationToken ct);
    Task<InstitutionResult2<InstitutionResult>> UpdateAsync(Guid id, InstitutionFields fields, Guid callerInstitutionId, bool callerIsPlatformOperator, CancellationToken ct);
    Task<InstitutionResult2<InstitutionResult>> WithdrawAsync(Guid id, CancellationToken ct);
    Task<InstitutionResult2<InstitutionResult>> ReactivateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DirectoryEntry>> SearchDirectoryAsync(string? query, CancellationToken ct);
}

public interface IInstitutionRepository
{
    Task<bool> DuplicateExistsAsync(string name, string contactEmail, string postalCode, CancellationToken ct); // BRL-003, includes inactive (E1)
    Task AddAsync(Domain.Entities.Institution institution, CancellationToken ct);
    Task<Domain.Entities.Institution?> FindByIdIgnoringTenantAsync(Guid id, CancellationToken ct); // PlatformOperator-scope operations need any institution, not just the caller's own
    Task<Domain.Entities.SubscriptionPlan?> FindEntryPlanAsync(CancellationToken ct);
    Task<IReadOnlyList<Domain.Entities.PublicInstitutionDirectoryProjection>> SearchDirectoryAsync(string? query, CancellationToken ct);
    Task UpsertDirectoryProjectionAsync(Domain.Entities.Institution institution, CancellationToken ct); // Tech Design A5: synchronous refresh on create/update/withdraw

    Task<Domain.Entities.IdempotencyRecord?> FindIdempotencyRecordAsync(string key, CancellationToken ct);
    Task AddIdempotencyRecordAsync(Domain.Entities.IdempotencyRecord record, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
