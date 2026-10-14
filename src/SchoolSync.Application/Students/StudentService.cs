using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Students;

// API-011/012/013/014/015. BRL-017 through BRL-021.
public class StudentService(IStudentRepository repository, IStudentFieldPolicy policy, TimeProvider clock, SchoolSync.Application.Commercial.IPlanEntitlements entitlements, SchoolSync.Application.Common.ISerializableTransaction transaction) : IStudentService
{
    // API-011. SPEC-FR-011 + SPEC-FR-012 (guardian fields arrive in this same payload).
    public async Task<StudentOutcome<StudentResult>> EnrolAsync(StudentFields fields, Guid institutionId, CancellationToken ct)
    {
        if (!policy.IsSatisfiedBy(fields, Today()))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.ValidationFailed);

        // SPEC-FR-011: "class must exist in caller's institution". The second half of that
        // sentence needs no code — the tenant query filter means a class belonging to another
        // institution is not visible to this query at all, so "exists" and "exists in the
        // caller's institution" are the same question here.
        if (!await repository.ClassExistsAsync(fields.ClassId, ct))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.ClassNotFound);

        if (await IdentifiersClashAsync(fields, excludingStudentId: null, ct))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.DuplicateIdentifier);

        // CT-503 / BRL-073 / TC-706: the tier capacity gate runs BEFORE anything is added — no grace
        // overage, not even one row. Last of the checks, so a request that is invalid anyway reports
        // its own error rather than a capacity one.
        //
        // CT-701 / U29: the gate and the insert run in ONE serializable transaction. Separately they
        // are a check-then-act race — two concurrent admissions at capacity − 1 both counted room
        // and both committed, putting the institution one seat over its tier with no error raised.
        var student = new Student
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            SrNumber = fields.SrNumber,
            RollNumber = fields.RollNumber,
            Name = fields.Name,
            DateOfBirth = fields.DateOfBirth,
            Gender = fields.Gender,
            Email = fields.Email,
            ClassId = fields.ClassId, // BRL-017: exactly one class placement
            GuardianFatherName = fields.GuardianFatherName,
            GuardianMotherName = fields.GuardianMotherName,
            GuardianContactEmail = fields.GuardianContactEmail,
            GuardianPhone = fields.GuardianPhone,
            IsActive = true
        };

        return await transaction.RunAsync(async inner =>
        {
            if (!await entitlements.HasLearnerCapacityAsync(institutionId, 1, inner))
                return StudentOutcome<StudentResult>.Fail(StudentFailureReason.CapacityExceeded);

            await repository.AddAsync(student, inner);
            await repository.SaveChangesAsync(inner);
            return StudentOutcome<StudentResult>.Ok(ToResult(student));
        }, ct);
    }

    // API-012. SPEC-FR-013 — amend details AND change class placement.
    public async Task<StudentOutcome<StudentResult>> AmendAsync(Guid id, StudentFields fields, CancellationToken ct)
    {
        var student = await repository.FindByIdAsync(id, ct);
        if (student is null) return StudentOutcome<StudentResult>.Fail(StudentFailureReason.NotFound);

        if (!policy.IsSatisfiedBy(fields, Today()))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.ValidationFailed);

        if (fields.ClassId != student.ClassId && !await repository.ClassExistsAsync(fields.ClassId, ct))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.ClassNotFound);

        // BRL-018 again on amend — excluding this student, or renaming a student to its own
        // existing SR number would report a duplicate against itself.
        if (await IdentifiersClashAsync(fields, excludingStudentId: student.Id, ct))
            return StudentOutcome<StudentResult>.Fail(StudentFailureReason.DuplicateIdentifier);

        student.SrNumber = fields.SrNumber;
        student.RollNumber = fields.RollNumber;
        student.Name = fields.Name;
        student.DateOfBirth = fields.DateOfBirth;
        student.Gender = fields.Gender;
        student.Email = fields.Email;
        student.GuardianFatherName = fields.GuardianFatherName;
        student.GuardianMotherName = fields.GuardianMotherName;
        student.GuardianContactEmail = fields.GuardianContactEmail;
        student.GuardianPhone = fields.GuardianPhone;

        // BRL-017: "reassignment replaces the prior placement, never adds a second". ClassId is a
        // single scalar FK, so replacement is the only expressible outcome — there is no shape in
        // which a second concurrent placement could exist. E3 is satisfied by the same fact from
        // the other side: nothing here touches historical attendance or enrollment rows, so a
        // mid-term move leaves prior-class history attached to the prior class.
        student.ClassId = fields.ClassId;

        await repository.SaveChangesAsync(ct);
        return StudentOutcome<StudentResult>.Ok(ToResult(student));
    }

    // API-013. SPEC-FR-014 — reversible, history intact.
    public async Task<StudentOutcome<StudentResult>> WithdrawAsync(Guid id, CancellationToken ct)
    {
        var student = await repository.FindByIdAsync(id, ct);
        if (student is null) return StudentOutcome<StudentResult>.Fail(StudentFailureReason.NotFound);

        // D8/BR-005 via CT-004's one canonical path — never `IsActive = false` inline. Attendance
        // and class-assignment rows are untouched, which is what "history intact and still
        // attributable" means in SPEC-FR-014.
        student.Deactivate(clock.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(ct);
        return StudentOutcome<StudentResult>.Ok(ToResult(student));
    }

    public async Task<StudentOutcome<StudentResult>> ReactivateAsync(Guid id, CancellationToken ct)
    {
        var student = await repository.FindByIdAsync(id, ct);
        if (student is null) return StudentOutcome<StudentResult>.Fail(StudentFailureReason.NotFound);

        // CT-503 / BRL-073: reactivating a withdrawn learner takes a seat again, so it is gated too
        // — and CT-701 / U29 applies here for the same reason it applies to admission: two
        // concurrent reactivations at capacity − 1 would otherwise both find room.
        if (student.IsActive)
        {
            student.Reactivate();
            await repository.SaveChangesAsync(ct);
            return StudentOutcome<StudentResult>.Ok(ToResult(student));
        }

        return await transaction.RunAsync(async inner =>
        {
            if (!await entitlements.HasLearnerCapacityAsync(student.InstitutionId, 1, inner))
                return StudentOutcome<StudentResult>.Fail(StudentFailureReason.CapacityExceeded);

            student.Reactivate();
            await repository.SaveChangesAsync(inner);
            return StudentOutcome<StudentResult>.Ok(ToResult(student));
        }, ct);
    }

    // API-015. SPEC-FR-016 — staff-initiated, optional link to a platform user account.
    public async Task<StudentOutcome<StudentResult>> LinkAccountAsync(Guid id, Guid userAccountId, CancellationToken ct)
    {
        var student = await repository.FindByIdAsync(id, ct);
        if (student is null) return StudentOutcome<StudentResult>.Fail(StudentFailureReason.NotFound);

        // Tenant-filtered: an account belonging to another institution is invisible here, so a
        // cross-institution link cannot be formed even with a valid account id.
        var account = await repository.FindUserAccountAsync(userAccountId, ct);
        if (account is null) return StudentOutcome<StudentResult>.Fail(StudentFailureReason.NotFound);

        // The link is held on UserAccount.StudentId (ENT-002, FR-016) — Student has no reciprocal
        // column, so this is the only side to write.
        account.StudentId = student.Id;
        await repository.SaveChangesAsync(ct);

        return StudentOutcome<StudentResult>.Ok(ToResult(student));
    }

    // API-014. BRL-020: institution scoping is the tenant filter's job, applied to every read
    // below without a per-method WHERE clause.
    public async Task<StudentResult?> FindAsync(Guid id, CancellationToken ct)
    {
        var student = await repository.FindByIdAsync(id, ct);
        return student is null ? null : ToResult(student);
    }

    public async Task<IReadOnlyList<StudentResult>> ListAsync(CancellationToken ct) =>
        (await repository.ListAsync(ct)).Select(ToResult).ToList();

    public async Task<IReadOnlyList<StudentResult>> RosterAsync(Guid classId, CancellationToken ct) =>
        (await repository.RosterAsync(classId, ct)).Select(ToResult).ToList();

    // BRL-018/TC-302: both identifiers are checked, each independently. An implementation that
    // checked only the roll number would still pass a happy-path enrolment test — TC-302 exists
    // precisely to fail it, so the SR-number half is not an incidental extra condition here.
    private async Task<bool> IdentifiersClashAsync(StudentFields fields, Guid? excludingStudentId, CancellationToken ct) =>
        await repository.SrNumberExistsAsync(fields.SrNumber, excludingStudentId, ct)
        || await repository.RollNumberExistsAsync(fields.RollNumber, excludingStudentId, ct);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static StudentResult ToResult(Student s) => new(
        s.Id, s.InstitutionId, s.SrNumber, s.RollNumber, s.Name, s.DateOfBirth, s.Gender, s.Email,
        s.ClassId, s.GuardianFatherName, s.GuardianMotherName, s.GuardianContactEmail, s.GuardianPhone,
        s.IsActive, s.DeactivatedAtUtc);
}
