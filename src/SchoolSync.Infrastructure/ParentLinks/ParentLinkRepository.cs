using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.ParentLinks;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.ParentLinks;

// Note on filtering: ParentLinkCode and ParentStudentLink are NOT ITenantOwned (04-data-model.md
// §2.12 — they hang off Student, which is), so no tenant filter applies to them directly. Student
// IS filtered, which is what scopes the staff-facing methods below. The single deliberate
// exception is FindCodeForConsumptionAsync — see ADR-0004.
public class ParentLinkRepository(SchoolSyncDbContext db) : IParentLinkRepository
{
    public Task<Student?> FindStudentAsync(Guid studentId, CancellationToken ct) =>
        db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);

    // "Issued" = unconsumed AND unrevoked AND unexpired, derived exactly as ParentLinkService
    // derives it. An already-expired code needs no invalidation, so it is left alone.
    public async Task<IReadOnlyList<ParentLinkCode>> FindIssuedCodesForStudentAsync(Guid studentId, DateTime nowUtc, CancellationToken ct) =>
        await db.ParentLinkCodes
            .Where(c => c.StudentId == studentId
                        && c.ConsumedAtUtc == null
                        && c.RevokedAtUtc == null
                        && c.ExpiresAtUtc > nowUtc)
            .ToListAsync(ct);

    // Scoped through the student so staff cannot revoke a code belonging to another institution:
    // the join to Students is tenant-filtered, so a foreign studentId yields nothing.
    public Task<ParentLinkCode?> FindCodeByIdAsync(Guid studentId, Guid codeId, CancellationToken ct) =>
        db.ParentLinkCodes
            .Where(c => c.Id == codeId && c.StudentId == studentId)
            .Where(c => db.Students.Any(s => s.Id == c.StudentId))
            .FirstOrDefaultAsync(ct);

    // ADR-0004 — the second and only other deliberate IgnoreQueryFilters() in the codebase after
    // ADR-0002's anonymous-auth case. A Parent submitting a code from another institution must
    // receive ERR-309 ("this code isn't valid for your school"), which is only distinguishable
    // from ERR-310 if the foreign student's institution can be read at all. Exactly one field
    // crosses the boundary — the owning institution id — and it is used only to REFUSE the
    // request; no student data is returned on any path.
    public async Task<(ParentLinkCode Code, Guid StudentInstitutionId)?> FindCodeForConsumptionAsync(string code, CancellationToken ct)
    {
        var row = await db.ParentLinkCodes
            .IgnoreQueryFilters()
            .Where(c => c.Code == code)
            .Select(c => new { Code = c, c.Student.InstitutionId })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.Code, row.InstitutionId);
    }

    public Task<bool> LinkExistsAsync(Guid parentUserId, Guid studentId, CancellationToken ct) =>
        db.ParentStudentLinks.AnyAsync(l => l.ParentUserId == parentUserId && l.StudentId == studentId, ct);

    public async Task AddCodeAsync(ParentLinkCode code, CancellationToken ct) =>
        await db.ParentLinkCodes.AddAsync(code, ct);

    public async Task AddLinkAsync(ParentStudentLink link, CancellationToken ct) =>
        await db.ParentStudentLinks.AddAsync(link, ct);

    // FR-074: the caller's own record, reached via UserAccount.StudentId (the link CT-102's
    // API-015 writes). Tenant-filtered on both sides.
    public async Task<Student?> FindStudentForUserAsync(Guid userAccountId, CancellationToken ct)
    {
        var studentId = await db.UserAccounts
            .Where(u => u.Id == userAccountId)
            .Select(u => u.StudentId)
            .FirstOrDefaultAsync(ct);

        return studentId is null ? null : await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
    }

    // FR-075/BR-066: linked children only. The join to Students is tenant-filtered, so even a
    // stale link pointing outside the parent's institution could not surface a foreign student.
    public async Task<IReadOnlyList<Student>> FindLinkedStudentsAsync(Guid parentUserId, CancellationToken ct) =>
        await db.ParentStudentLinks
            .Where(l => l.ParentUserId == parentUserId)
            .Join(db.Students, l => l.StudentId, s => s.Id, (_, s) => s)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
