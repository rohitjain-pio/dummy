using SchoolSync.Application.ParentLinks;
using SchoolSync.Domain.Entities;

namespace SchoolSync.IntegrationTests.Fakes;

// Double for the unit-layer parent-link tests (TC-305/306/307 are Layer=Unit).
//
// The one thing this MUST get right to be faithful: FindCodeForConsumptionAsync is cross-tenant
// by design (ADR-0004), so it resolves a code regardless of which institution owns the student,
// while every other method is tenant-scoped. StudentInstitutions is the stand-in for that
// boundary — a fake that scoped the consumption lookup too would make the ERR-309 path
// untestable and quietly agree with a broken implementation.
public class FakeParentLinkRepository : IParentLinkRepository
{
    public List<ParentLinkCode> Codes { get; } = [];
    public List<ParentStudentLink> Links { get; } = [];
    public List<Student> Students { get; } = [];

    // studentId -> owning institution. Populated for students in ANY institution, including ones
    // the caller cannot see.
    public Dictionary<Guid, Guid> StudentInstitutions { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<Student?> FindStudentAsync(Guid studentId, CancellationToken ct) =>
        Task.FromResult(Students.FirstOrDefault(s => s.Id == studentId));

    public Task<IReadOnlyList<ParentLinkCode>> FindIssuedCodesForStudentAsync(Guid studentId, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ParentLinkCode>>(Codes
            .Where(c => c.StudentId == studentId
                        && c.ConsumedAtUtc is null
                        && c.RevokedAtUtc is null
                        && c.ExpiresAtUtc > nowUtc)
            .ToList());

    public Task<ParentLinkCode?> FindCodeByIdAsync(Guid studentId, Guid codeId, CancellationToken ct) =>
        Task.FromResult(Codes.FirstOrDefault(c => c.Id == codeId && c.StudentId == studentId));

    public Task<(ParentLinkCode Code, Guid StudentInstitutionId)?> FindCodeForConsumptionAsync(string code, CancellationToken ct)
    {
        var found = Codes.FirstOrDefault(c => c.Code == code);
        if (found is null) return Task.FromResult<(ParentLinkCode, Guid)?>(null);

        var institutionId = StudentInstitutions.TryGetValue(found.StudentId, out var id) ? id : Guid.Empty;
        return Task.FromResult<(ParentLinkCode, Guid)?>((found, institutionId));
    }

    public Task<bool> LinkExistsAsync(Guid parentUserId, Guid studentId, CancellationToken ct) =>
        Task.FromResult(Links.Any(l => l.ParentUserId == parentUserId && l.StudentId == studentId));

    public Task AddCodeAsync(ParentLinkCode code, CancellationToken ct)
    {
        Codes.Add(code);
        return Task.CompletedTask;
    }

    public Task AddLinkAsync(ParentStudentLink link, CancellationToken ct)
    {
        Links.Add(link);
        return Task.CompletedTask;
    }

    public Task<Student?> FindStudentForUserAsync(Guid userAccountId, CancellationToken ct) =>
        Task.FromResult<Student?>(null);

    public Task<IReadOnlyList<Student>> FindLinkedStudentsAsync(Guid parentUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Student>>(Links
            .Where(l => l.ParentUserId == parentUserId)
            .Join(Students, l => l.StudentId, s => s.Id, (_, s) => s)
            .ToList());

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
