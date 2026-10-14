using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Students;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Students;

// Every query here runs under the global tenant query filter (T-002/D4). No method calls
// IgnoreQueryFilters() — there is no PlatformOperator cross-tenant path over student records
// (BR-013's bypass is not granted here), so "within the caller's institution" holds structurally
// for uniqueness checks, reads and lookups alike.
public class StudentRepository(SchoolSyncDbContext db) : IStudentRepository
{
    // BRL-018. Withdrawn students are included on purpose: the unique index is
    // (InstitutionId, SrNumber) with no IsActive filter, so excluding inactive rows here would
    // let the application accept a payload SQL Server then rejects.
    public Task<bool> SrNumberExistsAsync(string srNumber, Guid? excludingStudentId, CancellationToken ct)
    {
        var query = db.Students.Where(s => s.SrNumber == srNumber);
        if (excludingStudentId is { } id) query = query.Where(s => s.Id != id);
        return query.AnyAsync(ct);
    }

    public Task<bool> RollNumberExistsAsync(string rollNumber, Guid? excludingStudentId, CancellationToken ct)
    {
        var query = db.Students.Where(s => s.RollNumber == rollNumber);
        if (excludingStudentId is { } id) query = query.Where(s => s.Id != id);
        return query.AnyAsync(ct);
    }

    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) =>
        db.Classes.AnyAsync(c => c.Id == classId, ct);

    public async Task AddAsync(Student student, CancellationToken ct) =>
        await db.Students.AddAsync(student, ct);

    public Task<Student?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<UserAccount?> FindUserAccountAsync(Guid userAccountId, CancellationToken ct) =>
        db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userAccountId, ct);

    public async Task<IReadOnlyList<Student>> ListAsync(CancellationToken ct) =>
        await db.Students.OrderBy(s => s.RollNumber).ToListAsync(ct);

    public async Task<IReadOnlyList<Student>> RosterAsync(Guid classId, CancellationToken ct) =>
        await db.Students.Where(s => s.ClassId == classId).OrderBy(s => s.RollNumber).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
