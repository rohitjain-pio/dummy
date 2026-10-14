using SchoolSync.Application.Students;
using SchoolSync.Domain.Entities;

namespace SchoolSync.IntegrationTests.Fakes;

// A faithful double for the UNIT-layer student tests (14-test-spec/academic-records.md marks
// TC-302 as Layer=Unit). The real repository runs every query under the global tenant filter, so
// the set held here represents exactly one institution's visible rows — which is why no method
// below filters on InstitutionId. Modelling it any other way would make the double disagree with
// the thing it doubles.
//
// Withdrawn students are included in the uniqueness checks, matching StudentRepository and the
// unfiltered (InstitutionId, SrNumber) / (InstitutionId, RollNumber) unique indexes behind it.
public class FakeStudentRepository : IStudentRepository
{
    public List<Student> Students { get; } = [];
    public List<Guid> ExistingClassIds { get; } = [];
    public List<UserAccount> UserAccounts { get; } = [];
    public int SaveChangesCallCount { get; private set; }

    public Task<bool> SrNumberExistsAsync(string srNumber, Guid? excludingStudentId, CancellationToken ct) =>
        Task.FromResult(Students.Any(s => s.SrNumber == srNumber && s.Id != excludingStudentId));

    public Task<bool> RollNumberExistsAsync(string rollNumber, Guid? excludingStudentId, CancellationToken ct) =>
        Task.FromResult(Students.Any(s => s.RollNumber == rollNumber && s.Id != excludingStudentId));

    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) =>
        Task.FromResult(ExistingClassIds.Contains(classId));

    public Task AddAsync(Student student, CancellationToken ct)
    {
        Students.Add(student);
        return Task.CompletedTask;
    }

    public Task<Student?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Students.FirstOrDefault(s => s.Id == id));

    public Task<UserAccount?> FindUserAccountAsync(Guid userAccountId, CancellationToken ct) =>
        Task.FromResult(UserAccounts.FirstOrDefault(u => u.Id == userAccountId));

    public Task<IReadOnlyList<Student>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Student>>(Students.OrderBy(s => s.RollNumber).ToList());

    public Task<IReadOnlyList<Student>> RosterAsync(Guid classId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Student>>(Students.Where(s => s.ClassId == classId).OrderBy(s => s.RollNumber).ToList());

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
