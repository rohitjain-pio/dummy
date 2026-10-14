using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.ParentLinks;

// API-016/017/018/019. T-003's state machine, whose states are DERIVED from the three nullable
// timestamps on ParentLinkCode rather than stored as a status column (04-data-model.md §2.12):
//
//   Consumed  <- ConsumedAtUtc is not null   (terminal)
//   Revoked   <- RevokedAtUtc  is not null   (terminal)
//   Expired   <- now >= ExpiresAtUtc         (terminal, derived — never written)
//   Issued    <- none of the above
//
// Deriving Expired rather than writing it is what makes "expired" correct without a sweeper job;
// the cost is that every read must apply the same derivation, which is why it lives in the two
// helpers at the bottom and nowhere else.
public class ParentLinkService(
    IParentLinkRepository repository,
    IParentLinkCodeGenerator generator,
    TimeProvider clock) : IParentLinkService
{
    public const int ExpiryDays = 7; // CFG-005

    // API-016. The regenerate edge of the state machine lives here: issuing a second code while
    // one is still Issued invalidates the first, so a student never has two live codes.
    public async Task<ParentLinkOutcome<ParentLinkCodeIssued>> IssueCodeAsync(Guid studentId, Guid issuedByUserId, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // Tenant-filtered — staff cannot issue a code against another institution's student.
        var student = await repository.FindStudentAsync(studentId, ct);
        if (student is null) return ParentLinkOutcome<ParentLinkCodeIssued>.Fail(ParentLinkFailureReason.NotFound);

        // "Issued --[staff: regenerate, before consumption]--> Issued (new code, old code
        // invalidated)". Revoking is how invalidation is expressed, which also keeps the filtered
        // unique index on Code (unique WHERE unconsumed AND unrevoked) satisfiable.
        foreach (var live in await repository.FindIssuedCodesForStudentAsync(studentId, nowUtc, ct))
            live.RevokedAtUtc = nowUtc;

        var code = new ParentLinkCode
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Code = generator.Generate(),
            IssuedByUserId = issuedByUserId,
            IssuedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(ExpiryDays) // CFG-005
        };

        await repository.AddCodeAsync(code, ct);
        await repository.SaveChangesAsync(ct);

        return ParentLinkOutcome<ParentLinkCodeIssued>.Ok(new ParentLinkCodeIssued(code.Id, code.Code, code.ExpiresAtUtc));
    }

    // API-017. ERR-307 on an already-consumed code: consumption is terminal, so revocation is not
    // a way to undo it — "staff must instead remove the ParentStudentLink directly if unlinking
    // is needed (a separate, explicit staff action, not implied by code revocation)".
    public async Task<ParentLinkOutcome<bool>> RevokeCodeAsync(Guid studentId, Guid codeId, CancellationToken ct)
    {
        var code = await repository.FindCodeByIdAsync(studentId, codeId, ct);
        if (code is null) return ParentLinkOutcome<bool>.Fail(ParentLinkFailureReason.NotFound);

        if (code.ConsumedAtUtc is not null)
            return ParentLinkOutcome<bool>.Fail(ParentLinkFailureReason.AlreadyConsumed);

        // Revoking an already-revoked code is a no-op, not an error — the post-condition the
        // caller asked for already holds.
        code.RevokedAtUtc ??= clock.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(ct);
        return ParentLinkOutcome<bool>.Ok(true);
    }

    // API-018. The decision table in 07-business-logic/academic-records.md, in its own order —
    // the order is load-bearing, not incidental: an expired code returns ERR-308 even when the
    // institution also mismatches, because the table's expired row carries "—" in the
    // institution-match column and therefore wins.
    public async Task<ParentLinkOutcome<ParentLinkCreated>> ConsumeCodeAsync(
        string code, Guid parentUserId, Guid parentInstitutionId, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // ADR-0004: deliberately cross-tenant. If this lookup were tenant-filtered, a code issued
        // by another institution would simply be invisible and the only possible answer would be
        // ERR-310 — making ERR-309 unreachable and TC-308 unpassable.
        var found = await repository.FindCodeForConsumptionAsync(code, ct);
        if (found is null) return ParentLinkOutcome<ParentLinkCreated>.Fail(ParentLinkFailureReason.NotValid);

        var (linkCode, studentInstitutionId) = found.Value;

        // Table row 4: Revoked/Consumed -> ERR-310.
        if (IsTerminal(linkCode)) return ParentLinkOutcome<ParentLinkCreated>.Fail(ParentLinkFailureReason.NotValid);

        // Table row 2: Issued + expired -> ERR-308, institution column "—".
        if (IsExpired(linkCode, nowUtc)) return ParentLinkOutcome<ParentLinkCreated>.Fail(ParentLinkFailureReason.Expired);

        // Table row 3: Issued + unexpired + institution mismatch -> ERR-309. T-003's guard,
        // stopping a code leaked outside the institution from being used cross-tenant.
        if (studentInstitutionId != parentInstitutionId)
            return ParentLinkOutcome<ParentLinkCreated>.Fail(ParentLinkFailureReason.InstitutionMismatch);

        // Table row 1: consume.
        linkCode.ConsumedAtUtc = nowUtc;
        linkCode.ConsumedByUserId = parentUserId;

        // SILENCE (recorded in the build log): the specs never say what happens when a parent who
        // is ALREADY linked to this student redeems a further code for them. No ERR- covers it and
        // 08-error-catalogue.md is closed, so inventing one would be worse than the conservative
        // reading: the code is still consumed (it was validly presented) and the existing link is
        // returned unchanged, rather than inserting a duplicate that the composite primary key
        // (ParentUserId, StudentId) would reject anyway.
        if (!await repository.LinkExistsAsync(parentUserId, linkCode.StudentId, ct))
        {
            await repository.AddLinkAsync(new ParentStudentLink
            {
                ParentUserId = parentUserId,
                StudentId = linkCode.StudentId,
                CreatedAtUtc = nowUtc
            }, ct);
        }

        await repository.SaveChangesAsync(ct);
        return ParentLinkOutcome<ParentLinkCreated>.Ok(new ParentLinkCreated(linkCode.StudentId, nowUtc));
    }

    // API-019 (Student half) — FR-074, scoped to the caller's own record.
    public async Task<LinkedStudent?> FindOwnStudentAsync(Guid callerUserId, CancellationToken ct)
    {
        var student = await repository.FindStudentForUserAsync(callerUserId, ct);
        return student is null ? null : ToLinkedStudent(student);
    }

    // API-019 (Parent half) — FR-075/BR-066, linked children only, never another learner's data.
    public async Task<IReadOnlyList<LinkedStudent>> ListLinkedStudentsAsync(Guid parentUserId, CancellationToken ct) =>
        (await repository.FindLinkedStudentsAsync(parentUserId, ct)).Select(ToLinkedStudent).ToList();

    private static bool IsTerminal(ParentLinkCode code) =>
        code.ConsumedAtUtc is not null || code.RevokedAtUtc is not null;

    private static bool IsExpired(ParentLinkCode code, DateTime nowUtc) => nowUtc >= code.ExpiresAtUtc;

    private static LinkedStudent ToLinkedStudent(Student s) =>
        new(s.Id, s.Name, s.RollNumber, s.ClassId, s.IsActive);
}
