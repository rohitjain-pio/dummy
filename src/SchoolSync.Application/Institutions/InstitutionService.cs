using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Institutions;

// API-001/002/003(shared with configuration-commercial)/004/004b/005.
// Idempotency-Key handling (API-001) lives in the controller, not here — it's a transport-layer
// concern (a repeated HTTP request), not a business rule; this service is called only once per
// genuinely-new registration.
public class InstitutionService(IInstitutionRepository repository, TimeProvider clock) : IInstitutionService
{
    public async Task<InstitutionResult2<InstitutionResult>> RegisterAsync(InstitutionFields fields, CancellationToken ct)
    {
        // BRL-004/VAL-001/003/004 first (PF-001 step 2): a payload with an invalid field is
        // rejected as ERR-901 before it is ever compared for duplication.
        if (!InstitutionFieldPolicy.IsSatisfiedBy(fields))
            return InstitutionResult2<InstitutionResult>.Fail(InstitutionFailureReason.ValidationFailed);

        // BRL-003/VAL-002: composite check, all three fields together — never single-field
        // uniqueness (TC-102's adversarial point) — and includes withdrawn institutions (E1,
        // TC-103's adversarial point), which is why FindDuplicate is IgnoreQueryFilters-free but
        // still must not silently exclude IsActive=false rows.
        if (await repository.DuplicateExistsAsync(fields.Name, fields.ContactEmail, fields.PostalCode, ct))
            return InstitutionResult2<InstitutionResult>.Fail(InstitutionFailureReason.Duplicate);

        // BRL-074/Q-211/PF-001: the Entry/Free plan is assigned in the SAME operation that
        // creates the institution — there is no intermediate state where SubscriptionPlanId
        // could be read as null by any other request, because both writes happen inside the one
        // SaveChangesAsync call below (one DB transaction).
        var entryPlan = await repository.FindEntryPlanAsync(ct)
            ?? throw new InvalidOperationException(
                "No Entry/Free SubscriptionPlan is seeded — BRL-061 requires exactly 3 seed rows; this is a data-seeding defect, not a caller error.");

        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = fields.Name,
            RegistrationNumber = fields.RegistrationNumber,
            ContactEmail = fields.ContactEmail,
            Telephone = fields.Telephone,
            PostalAddress = fields.PostalAddress,
            PostalCode = fields.PostalCode,
            City = fields.City,
            Region = fields.Region,
            SubscriptionPlanId = entryPlan.Id,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            IsActive = true
        };

        await repository.AddAsync(institution, ct);
        await repository.UpsertDirectoryProjectionAsync(institution, ct); // A5: synchronous refresh
        await repository.SaveChangesAsync(ct);

        return InstitutionResult2<InstitutionResult>.Ok(ToResult(institution));
    }

    public async Task<InstitutionResult2<InstitutionResult>> UpdateAsync(
        Guid id, InstitutionFields fields, Guid callerInstitutionId, bool callerIsPlatformOperator, CancellationToken ct)
    {
        // BRL-002/E2: 403, not 404, on a scope mismatch — existence is never leaked to an
        // unauthorized caller (TC-104's adversarial point: must check role AND institution
        // match, not role alone).
        if (!callerIsPlatformOperator && callerInstitutionId != id)
            return InstitutionResult2<InstitutionResult>.Fail(InstitutionFailureReason.Forbidden);

        var institution = await repository.FindByIdIgnoringTenantAsync(id, ct);
        if (institution is null)
            return InstitutionResult2<InstitutionResult>.Fail(
                callerIsPlatformOperator ? InstitutionFailureReason.NotFound : InstitutionFailureReason.Forbidden);

        institution.Name = fields.Name;
        institution.RegistrationNumber = fields.RegistrationNumber;
        institution.ContactEmail = fields.ContactEmail;
        institution.Telephone = fields.Telephone;
        institution.PostalAddress = fields.PostalAddress;
        institution.PostalCode = fields.PostalCode;
        institution.City = fields.City;
        institution.Region = fields.Region;

        await repository.UpsertDirectoryProjectionAsync(institution, ct);
        await repository.SaveChangesAsync(ct);

        return InstitutionResult2<InstitutionResult>.Ok(ToResult(institution));
    }

    public async Task<InstitutionResult2<InstitutionResult>> WithdrawAsync(Guid id, CancellationToken ct)
    {
        var institution = await repository.FindByIdIgnoringTenantAsync(id, ct);
        if (institution is null) return InstitutionResult2<InstitutionResult>.Fail(InstitutionFailureReason.NotFound);

        institution.Deactivate(clock.GetUtcNow().UtcDateTime); // BR-005 — reversible, never a hard delete
        await repository.SaveChangesAsync(ct);
        return InstitutionResult2<InstitutionResult>.Ok(ToResult(institution));
    }

    public async Task<InstitutionResult2<InstitutionResult>> ReactivateAsync(Guid id, CancellationToken ct)
    {
        var institution = await repository.FindByIdIgnoringTenantAsync(id, ct);
        if (institution is null) return InstitutionResult2<InstitutionResult>.Fail(InstitutionFailureReason.NotFound);

        institution.Reactivate(); // restores exactly the pre-deactivation state, no re-validation
        await repository.SaveChangesAsync(ct);
        return InstitutionResult2<InstitutionResult>.Ok(ToResult(institution));
    }

    public async Task<IReadOnlyList<DirectoryEntry>> SearchDirectoryAsync(string? query, CancellationToken ct)
    {
        var rows = await repository.SearchDirectoryAsync(query, ct);
        // Never contactEmail/postalAddress/registrationNumber (NFR-016's bounded exception,
        // TC-105's adversarial point) — enforced by DirectoryEntry's shape itself, not by
        // remembering to omit fields at each call site.
        return rows.Select(r => new DirectoryEntry(r.InstitutionId, r.Name, r.City, r.Region)).ToList();
    }

    private static InstitutionResult ToResult(Institution i) => new(
        i.Id, i.Name, i.RegistrationNumber, i.ContactEmail, i.Telephone, i.PostalAddress,
        i.PostalCode, i.City, i.Region, i.IsActive, i.DeactivatedAtUtc, i.SubscriptionPlanId);
}
